using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeatherZillaData;

namespace WeatherZilla.Pages
{
    public class AddcityModel : PageModel
    {
        public string CityName { get; set; }
        public string Temperature { get; private set; }
        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private IEnumerable<WeatherData>? _airTemp;

        public AddcityModel()
        {
            CityName = "Lycksele";
            Temperature = "Unknown";
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
            IEnumerable<WeatherData?> airTemp = _airTemp ?? await GetAirTempAsync();
            string? temperature = airTemp?.First()?.TemperatureC.ToString();
            return temperature is null ? "" : temperature;
        }

        private async Task<IEnumerable<WeatherData>> GetAirTempAsync()
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

                // Demo API call; get temperature in Celsius for Lycksele
                // TODO: Read address from configuration (Azure feature..?)
                // TODO: Validate CityName string
                string address = $"https://weatherzillawebapi.azure-api.net/WeatherData?place={CityName}";
                _airTemp = await _client.GetFromJsonAsync<IEnumerable<WeatherData>>(address);
                if (_airTemp is null)
                {
                    _airTemp = Enumerable.Range(1, 1).Select(index => new WeatherZillaData.WeatherData { }).ToArray();
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