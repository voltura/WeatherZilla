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
        private WeatherData? _airTempGeo;
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

        public async Task<IActionResult> OnGetAsync(double? longitude, double? latitude)
        {
            Place = "Lycksele";
            if (longitude != null && latitude != null)
            {
                Temperature = await UseTemperatureFromGeoLocation((double)longitude, (double)latitude);
            }
            else
            {
                Temperature = await UseTemperature();
            }
            return Page();
        }

        public async Task<string> UseTemperatureFromGeoLocation(double longitude, double latitude)
        {
            WeatherData? airTempGeo = (WeatherData?)(_airTempGeo ?? await GetAirTempAsyncFromGeoLocation(longitude, latitude));
            string? temperature = airTempGeo?.TemperatureC.ToString();
            Place = airTempGeo?.Place is null ? "Unknown" : airTempGeo.Place;
            return temperature is null ? "" : temperature;
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
                    DebugData = $"Tried to read application configuration key 'WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_PLACE_URL'; it returned {(string.IsNullOrWhiteSpace(weatherDataForPlaceUrl) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_PLACE_URL + "'" : "'" + weatherDataForPlaceUrl + "'")}.";
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

        private async Task<WeatherZilla.Shared.Interfaces.IWeatherData> GetAirTempAsyncFromGeoLocation(double longitude, double latitude)
        {
            if (_airTempGeo != null)
            {
                return await Task.FromResult(_airTempGeo);
            }

            await _lock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_airTempGeo != null)
                {
                    return _airTempGeo;
                }

                // Read web api address from Azure configuration (set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github)
                string weatherDataForGeoLocationUrl = _configuration["WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_GEOLOCATION_URL"];
                // DEBUG: Show debug info
                DebugData = $"Tried to read application configuration key 'WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_GEOLOCATION_URL' it returned {(string.IsNullOrWhiteSpace(weatherDataForGeoLocationUrl) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_GEOLOCATION_URL + "'" : "'" + weatherDataForGeoLocationUrl + "'")}.";
                string address = $"{(string.IsNullOrWhiteSpace(weatherDataForGeoLocationUrl) ? WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_GEOLOCATION_URL : weatherDataForGeoLocationUrl)}";
                address = string.Format(address, longitude.ToString().Replace(',', '.'), latitude.ToString().Replace(',', '.'));
                // Demo API call; get temperature in Celsius for Lycksele
                _airTempGeo = await _client.GetFromJsonAsync<WeatherData>(address);

                if (_airTempGeo is null)
                {
                    // DEBUG: Show debug info
                    DebugData = "Could not retreive weather data.";
                    _airTempGeo = new WeatherData();
                }
                return _airTempGeo;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}