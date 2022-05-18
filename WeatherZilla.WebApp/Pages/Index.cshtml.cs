using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Web;
using WeatherZilla.Shared.Data;

namespace WeatherZilla.WebApp.Pages
{
    public class IndexModel : PageModel
    {
        public string Place { get; set; }
        public string? DebugData { get; set; }
        public string Temperature { get; private set; }

        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private IEnumerable<WeatherData>? _airTemp;
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            Place = "Lycksele";
            Temperature = "Unknown";
            _logger = logger;
            _configuration = configuration;
            _client = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Place = "Lycksele";
            Temperature = await UseTemperature();
            return Page();
        }

        public async Task<string> UseTemperature()
        {
            IEnumerable<WeatherZilla.Shared.Interfaces.IWeatherData?> airTemp = _airTemp ?? await GetAirTempAsync();
            string? temperature = airTemp?.First()?.TemperatureC.ToString();
            return temperature is null ? "" : temperature;
        }

        private async Task<IEnumerable<WeatherZilla.Shared.Interfaces.IWeatherData>> GetAirTempAsync()
        {
            if (_airTemp != null)
            {
                return await Task.FromResult(_airTemp);
            }

            await _lock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_airTemp != null)
                {
                    return _airTemp;
                }

                // TODO: Validate Place string
                bool validPlace = Place.All(c => Char.IsLetterOrDigit(c));
                if (!validPlace)
                {
                    _logger.LogDebug("Place '{Place}' not valid.", Place);
                }
                else
                {
                    Place = HttpUtility.UrlEncode(Place);
                    // Read web api address from Azure configuration (set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github)
                    string weatherDataForPlaceUrl = _configuration["WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_PLACE_URL"];
                    // DEBUG: Show debug info
                    DebugData = $"Tried to read application configuration key 'WeatherDataUrls:WeatherDataForPlace' it returned {(string.IsNullOrWhiteSpace(weatherDataForPlaceUrl) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_PLACE_URL + "'" : "'" + weatherDataForPlaceUrl + "'")}.";
                    string address = $"{(string.IsNullOrWhiteSpace(weatherDataForPlaceUrl) ? WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_PLACE_URL : weatherDataForPlaceUrl)}{Place}";
                    // Demo API call; get temperature in Celsius for Lycksele
                    _airTemp = await _client.GetFromJsonAsync<IEnumerable<WeatherData>>(address);
                }

                if (_airTemp is null)
                {
                    // DEBUG: Show debug info
                    DebugData = "Could not retreive weather data.";
                    _airTemp = Enumerable.Range(1, 1).Select(index => new WeatherData { }).ToArray();
                }
                return _airTemp;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}