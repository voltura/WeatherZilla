using Microsoft.AspNetCore.Mvc;
using WeatherZilla.Shared.Data;

namespace WeatherZilla.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StationsController : ControllerBase
    {
        private readonly ILogger<StationsController> _logger; // TODO: Use log feature

        public StationsController(ILogger<StationsController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetStationData")]
        public StationsData Get()
        {
            return new StationsData
            {
                Date = DateTime.Now,
                StationsId = 148330,
                StationsName = "Lycksele A",
                Active = true,
            };
        }
    }
}
