#region Using statements

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using WeatherZilla.Shared.Data;
using WeatherZilla.WebAPI.SmhiLatestHourAirTempData;
using WeatherZilla.WebAPI.SmhiStationData;

#endregion Using statements

namespace WeatherZilla.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherDataController : ControllerBase
    {
        #region Properties

        public string? DebugData { get; set; }

        #endregion Properties

        #region Injections

        private readonly ILogger<WeatherDataController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache; // NOTE: Implemented memory cache usage as per description in this blog post https://thecodeblogger.com/2021/06/07/how-to-use-in-memory-caching-for-net-core-web-apis/
                                                    //       There are other considerations to make if this is to be production code when it comes to cache size and cleanup, see above post for more info
                                                    //       More about data handling including cache handling see MS documentation here: https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/work-with-data-in-asp-net-core-apps

        #endregion Injections

        #region Private variables

        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private SmhiLatestHourAirTemp? _airTemp;
        private static readonly SemaphoreSlim _stationLock = new(1, 1);
        private SmhiStations? _stations;

        #endregion Private variables

        #region Constructor

        public WeatherDataController(ILogger<WeatherDataController> logger, IConfiguration configuration, IMemoryCache memoryCache)
        {
            _logger = logger;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _client = new();
            _logger.LogDebug("Created HTTPClient");
        }

        #endregion Constructor

        #region Public web methods

        [HttpGet("GetWeatherData")]
        public async Task<IEnumerable<WeatherData>> GetAsync(string place)
        {
            var stationDataMemoryCacheKeyForPlace = Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY + place;
            // If found in cache, return cached data
            if (_memoryCache.TryGetValue(stationDataMemoryCacheKeyForPlace, out List<WeatherData> weatherDataCollection))
            {
                _logger.LogDebug("Using cached, not live, SMHI station airtemp data for place {place}.", place);
                return weatherDataCollection;
            }

            SmhiLatestHourAirTemp? airTemp = _airTemp ?? await GetAirTempAsync(place);
            string? temperature = airTemp?.ValueData?[0]?.RoundedValue;
            DateTime weatherDateTime = Data.SmhiDateHelper.GetDateTimeFromSmhiDate(airTemp?.ValueData?[0]?.Date);
            weatherDataCollection = new()
            {
                 new WeatherData()
                {
                    Date = weatherDateTime,
                    TemperatureC = Convert.ToInt32(temperature),
                    Place = airTemp?.Station?.Name is null ? place : airTemp.Station.Name,
                    Longitude = airTemp?.Position?[0].Longitude is null ? 0 : airTemp.Position[0].Longitude,
                    Latitude = airTemp?.Position?[0].Latitude is null ? 0 : airTemp.Position[0].Latitude
                }
            };

            // Set cache options
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(120));

            // Set object in cache
            _memoryCache.Set(stationDataMemoryCacheKeyForPlace, weatherDataCollection, cacheOptions);

            return weatherDataCollection;
        }

        [HttpGet("GetStationData")]
        public async Task<IEnumerable<StationsData>> GetAsync()
        {
            // If found in cache, return cached data
            if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<StationsData> stationDataCollection))
            {
                _logger.LogDebug("Using cached, not live, SMHI station data.");
                return stationDataCollection;
            }

            stationDataCollection = GetStationDataList(_stations ?? await GetStationsAsync());

            // Set cache options
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));

            // Set object in cache
            _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);
            return stationDataCollection;
        }

        #endregion Public web methods

        #region Private methods

        private static List<StationsData> GetStationDataList(SmhiStations? stations)
        {
            List<StationsData> stationDatas = new();
            DateTime stationDateTime;
            stations?.Station?.ForEach(smhiStation =>
            {
                // Only use active weather stations
                if (smhiStation.Active)
                {
                    // Get updated datetime for station
                    stationDateTime = Data.SmhiDateHelper.GetDateTimeFromSmhiDate(smhiStation.Updated);
                    stationDatas.Add(new StationsData()
                    {
                        StationsId = smhiStation.Id,
                        StationsName = smhiStation.Name,
                        Date = stationDateTime
                    });
                }
            });
            return stationDatas;
        }

        private async Task<SmhiLatestHourAirTemp> GetAirTempAsync(string place)
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

                place = HttpUtility.UrlDecode(place).ToLowerInvariant();

                if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<StationsData> stationDataCollection))
                {
                    _logger.LogDebug("Using cached stationdata");
                }
                else
                {
                    stationDataCollection = GetStationDataList(_stations ?? await GetStationsAsync());
                    MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));
                    _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);
                }
                StationsData? matchingPlace = stationDataCollection.Find(x => x.StationsName != null && x.StationsName.ToLowerInvariant().StartsWith(place));

                if (matchingPlace is null)
                {
                    _logger.LogDebug("Could not find a station for {place}", place);
                }
                else
                {
                    var smhiStationID = matchingPlace.StationsId.ToString();

                    string? smhiLatestHourAirTempJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_LATEST_HOUR_AIRTEMP_URL"];
                    if (string.IsNullOrWhiteSpace(smhiLatestHourAirTempJsonAddress))
                    {
                        smhiLatestHourAirTempJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL;
                    }

                    // API call; get temperature in Celsius for SMHI station with identifier smhiStationID)
                    string smhiLatestHourDataFromStationJsonAddress = string.Format(smhiLatestHourAirTempJsonAddress, smhiStationID);

                    _airTemp = await _client.GetFromJsonAsync<SmhiLatestHourAirTemp>(smhiLatestHourDataFromStationJsonAddress);
                }

                _airTemp = (_airTemp is null) ? new SmhiLatestHourAirTemp() : _airTemp;
                return _airTemp;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<SmhiStations> GetStationsAsync()
        {
            if (_stations != null)
            {
                return await Task.FromResult(_stations);
            }

            await _stationLock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_stations != null)
                {
                    return _stations;
                }

                // Read address from Application Configuration if available - otherwise use default 
                // INFO: This value is also set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github to Azure (WebApp environment) during production deployment
                string? smhiStationsJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_STATIONS_URL"];

                // DEBUG: Log debug data
                DebugData = $"Tried to read application configuration key 'SMHI_URLS:SMHI_JSON_STATIONS_URL' in Azure and it returned {(string.IsNullOrWhiteSpace(smhiStationsJsonAddress) ? "nothing; using default value '" + Shared.Constants.DEFAULT_SMHI_JSON_STATIONS_URL + "'" : "'" + smhiStationsJsonAddress + "'")}.";
                _logger.LogDebug("{DebugData}", DebugData);

                if (string.IsNullOrWhiteSpace(smhiStationsJsonAddress))
                {
                    smhiStationsJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_STATIONS_URL;
                }

                // Call SMHI with HttpClient to get Json into class Data.SmhiStations (which should be matching their data)
                _stations = await _client.GetFromJsonAsync<SmhiStations>(smhiStationsJsonAddress);

                _stations = (_stations is null) ? new SmhiStations() : _stations;
                return _stations;
            }
            finally
            {
                _stationLock.Release();
            }
        }

        #endregion Private methods
    }
}