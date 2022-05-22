#region Using statements

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Web;
using WeatherZilla.Shared.Data;
using WeatherZilla.Shared.Interfaces;

#endregion Using statements

namespace WeatherZilla.WebApp.Pages
{
    public class IndexModel : PageModel
    {
        #region Public properties

        public string Place { get; set; }
        public string? DebugData { get; set; }
        public string Temperature { get; private set; }

        #endregion Public properties

        #region Private variables

        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private IWeatherData? _airTemp;
        private IWeatherData? _airTempGeo;

        #endregion Private variables

        #region Injections

        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        #endregion Injections

        #region Constructor

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            Place = "Lycksele";
            Temperature = "Unknown";
            _logger = logger;
            _configuration = configuration;
            _client = new();
        }

        #endregion Constructor

        #region Public methods

        public async Task<IActionResult> OnGetAsync(double? longitude, double? latitude)
        {
            Place = "Lycksele";
            if (longitude != null && latitude != null) Temperature = await UseTemperatureFromGeoLocation((double)longitude, (double)latitude);
            else Temperature = await UseTemperature();
            return Page();
        }

        #endregion Public methods

        #region Private methods

        private async Task<string> UseTemperatureFromGeoLocation(double longitude, double latitude)
        {
            IWeatherData? airTempGeo = _airTempGeo ?? await GetAirTempAsyncFromGeoLocation(longitude, latitude);
            string? temperature = airTempGeo?.TemperatureC.ToString();
            Place = airTempGeo?.Place is null ? "Unknown" : airTempGeo.Place;
            // TODO: Check if logic is correct to make class variable _airTempGeo null here - otherwise we will not get correct temperature... Maybe here use Memory Cache as in API as well?
            _airTempGeo = null;
            return temperature is null ? "" : temperature;
        }

        private async Task<string> UseTemperature()
        {
            IWeatherData? airTemp = _airTemp ?? await GetAirTempAsync();
            string? temperature = airTemp?.TemperatureC.ToString();
            // TODO: Check if logic is correct to make class variable _airTemp null here - otherwise we will not get correct temperature... Maybe here use Memory Cache as in API as well?
            _airTemp = null;
            return temperature is null ? "" : temperature;
        }

        private async Task<IWeatherData?> GetAirTempAsync()
        {
            if (_airTemp != null) return await Task.FromResult(_airTemp);
            await _lock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_airTemp != null) return _airTemp;
                // TODO: Validate Place string
                bool validPlace = Place.All(c => Char.IsLetterOrDigit(c) || c == '-' || c == ' ');
                if (!validPlace) _logger.LogDebug("Place '{Place}' not valid.", Place);
                else
                {
                    try
                    {
                        Place = HttpUtility.UrlEncode(Place);
                        // Read web api address from Azure configuration (set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github)
                        string weatherDataForPlaceUrl = _configuration["WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_PLACE_URL"];
                        // DEBUG: Show debug info
                        DebugData = $"Tried to read application configuration key 'WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_PLACE_URL'; it returned {(string.IsNullOrWhiteSpace(weatherDataForPlaceUrl) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_PLACE_URL + "'" : "'" + weatherDataForPlaceUrl + "'")}.";
                        string address = $"{(string.IsNullOrWhiteSpace(weatherDataForPlaceUrl) ? WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_PLACE_URL : weatherDataForPlaceUrl)}{Place}";
                        // Demo API call; get temperature in Celsius for Lycksele
                        _airTemp = await _client.GetFromJsonAsync<WeatherData>(address);
                    }
                    catch (Exception ex)
                    {
                        DebugData = ex.Message;
                    }
                }
                // DEBUG: Show debug info
                if (_airTemp is null) DebugData = "Could not retreive weather data.";
                return _airTemp;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<IWeatherData?> GetAirTempAsyncFromGeoLocation(double longitude, double latitude)
        {
            if (_airTempGeo != null) return await Task.FromResult(_airTempGeo);
            await _lock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_airTempGeo != null) return _airTempGeo;
                // Read web api address from Azure configuration (set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github)
                string weatherDataForGeoLocationUrl = _configuration["WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_GEOLOCATION_URL"];
                // DEBUG: Show debug info
                DebugData = $"Tried to read application configuration key 'WEATHERZILLA_WEBAPI_URLS:WEATHERDATA_FOR_GEOLOCATION_URL' it returned {(string.IsNullOrWhiteSpace(weatherDataForGeoLocationUrl) ? "nothing; using default value '" + WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_GEOLOCATION_URL + "'" : "'" + weatherDataForGeoLocationUrl + "'")}.";
                string address = $"{(string.IsNullOrWhiteSpace(weatherDataForGeoLocationUrl) ? WeatherZilla.Shared.Constants.DEFAULT_WEATHERDATA_FOR_GEOLOCATION_URL : weatherDataForGeoLocationUrl)}";
                address = string.Format(address, longitude.ToString().Replace(',', '.'), latitude.ToString().Replace(',', '.'));
                // Demo API call; get temperature in Celsius for Lycksele
                _airTempGeo = await _client.GetFromJsonAsync<WeatherData>(address);
                // DEBUG: Show debug info
                if (_airTempGeo is null) DebugData = "Could not retreive weather data for position.";
            }
            catch (Exception ex)
            {
                DebugData = ex.Message;
            }
            finally
            {
                _lock.Release();
            }
            return _airTempGeo;
        }
    }

    #endregion Private methods
}