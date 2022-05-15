using Microsoft.AspNetCore.Mvc;
using WeatherZilla.Shared.Data;

namespace WeatherZilla.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherTestController : ControllerBase
    {
        private readonly ILogger<WeatherTestController> _logger; // TODO: Use log feature

        public WeatherTestController(ILogger<WeatherTestController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetTestWeatherData")]
        public WeatherData Get()
        {
            return new WeatherData
            {
                Date = DateTime.Now,
                TemperatureC = 29,
                Place = "Stockholm",
                Summary = "Sunny",
                Longitude = 59.3167,
                Latitude = 18.06
            };
        }
    }
}