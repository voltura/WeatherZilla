using Microsoft.AspNetCore.Mvc;
using WeatherZilla.Shared.Data;

namespace WeatherZilla.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherDataController : ControllerBase
    {
        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private Data.SmhiLatestHourAirTemp? _airTemp;

        private readonly ILogger<WeatherDataController> _logger; // TODO: Use log feature

        public WeatherDataController(ILogger<WeatherDataController> logger)
        {
            _logger = logger;
            _client = new();
        }

        [HttpGet(Name = "GetWeatherData")]
        public async Task<IEnumerable<WeatherData>> GetAsync(string place)
        {
            Data.SmhiLatestHourAirTemp? airTemp = _airTemp ?? await GetAirTempAsync(place);
            string? temperature = airTemp?.ValueData?[0]?.RoundedValue;

            DateTime weatherDateTime = DateTime.Now;
            if (airTemp != null && airTemp.Period != null)
            {
                weatherDateTime = DateTime.FromFileTimeUtc(airTemp.Period.To);
            }
            return Enumerable.Range(1, 1).Select(index => new WeatherData
            {
                Date = weatherDateTime,
                TemperatureC = Convert.ToInt32(temperature),
                Place = airTemp?.Station?.Name is null ? place : airTemp.Station.Name,
                Longitude = airTemp?.Position?[0].Longitude is null ? 0 : airTemp.Position[0].Longitude,
                Latitude = airTemp?.Position?[0].Latitude is null ? 0 : airTemp.Position[0].Latitude
            })
            .ToArray();
        }

        private async Task<Data.SmhiLatestHourAirTemp> GetAirTempAsync(string place)
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
                // TODO: Handle 'place' parameter, now hardcoded... SMHI has a service to get list of weather stations with name and ID, match name from param 'place' to get ID to use in below API call
                var lyckseleSmhiStationID = "148330";
                // Demo API call; get temperature in Celsius for Lycksele (SMHI station with ID 148330)

                // TODO: Add to config
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