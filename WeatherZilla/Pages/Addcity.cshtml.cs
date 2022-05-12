using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.Pages
{
    public class AddcityModel : PageModel
    {
        public string CityName { get; set; }
        public string Temperature { get; private set; }
        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private Data.SmhiLatestHourAirTemp? _airTemp;

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
            Data.SmhiLatestHourAirTemp? airTemp = _airTemp ?? await GetAirTempAsync();
            string? temperature = airTemp?.ValueData?[0]?.RoundedValue;
            return temperature is null ? "" : temperature;
        }

        private async Task<Data.SmhiLatestHourAirTemp> GetAirTempAsync()
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

                var lyckseleSmhiStationID = "148330";
                // Demo API call; get temperature in Celsius for Lycksele (SMHI station with ID 148330)
                string address = $"https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1/station/{lyckseleSmhiStationID}/period/latest-hour/data.json";
                _airTemp = await _client.GetFromJsonAsync<Data.SmhiLatestHourAirTemp>(address);
                if (_airTemp is null)
                {
                    _airTemp = new Data.SmhiLatestHourAirTemp();
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