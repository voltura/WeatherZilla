using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeatherZilla.Shared.Data;

namespace WeatherZilla.WebApp.Pages
{
    public class IndexModel : PageModel
    {
        public string CityName { get; set; }
        public string DebugData { get; set; }
        public string Temperature { get; private set; }

        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private IEnumerable<WeatherData>? _airTemp;
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            DebugData = "";
            CityName = "Lycksele";
            Temperature = "Unknown";
            _logger = logger;
            _configuration = configuration;
            _client = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            CityName = "Lycksele";
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

                // TODO: Validate CityName string
                // Read web api address from Azure configuration (set by action WeatherZillaWebApp.yml in github)
                string weatherDataForPlace = _configuration["WeatherDataUrls:WeatherDataForPlace"];
                // DEBUG: Show debug info
                DebugData = $"Tried to read application configuration key 'WeatherDataUrls:WeatherDataForPlace' from Azure and it returned {(string.IsNullOrWhiteSpace(weatherDataForPlace) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.WEATHERDATA_FOR_PLACE_DEFAULT_URL + "'" : "'" + weatherDataForPlace + "'")}.";
                string address = $"{((string.IsNullOrWhiteSpace(weatherDataForPlace)) ? WeatherZilla.Shared.Constants.WEATHERDATA_FOR_PLACE_DEFAULT_URL : weatherDataForPlace)}{CityName}";
                // Demo API call; get temperature in Celsius for Lycksele
                _airTemp = await _client.GetFromJsonAsync<IEnumerable<WeatherData>>(address);


                string? connectionString = _configuration.GetConnectionString("aspnet-WeatherZilla-db_ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Development...
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