#region Using statements

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using WeatherZilla.Shared.Data;
using WeatherZilla.Shared.Interfaces;
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
        private static readonly SemaphoreSlim _stationLock = new(1, 1);

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
        public async Task<IWeatherData?> GetAsync(string place)
        {
            string stationDataMemoryCacheKeyForPlace = Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY + place;
            // If found in cache, return cached data
            if (_memoryCache.TryGetValue(stationDataMemoryCacheKeyForPlace, out WeatherData? weatherData))
            {
                _logger.LogDebug("Using cached, not live, SMHI station airtemp data for place {place}.", place);
                return weatherData;
            }
            SmhiLatestHourAirTemp? airTemp = await GetAirTempAsync(place);
            string? temperature = airTemp?.ValueData?[0]?.RoundedValue;
            DateTime weatherDateTime = Data.SmhiDateHelper.GetDateTimeFromSmhiDate(airTemp?.ValueData?[0]?.Date);
            weatherData = new()
            {
                Date = weatherDateTime,
                TemperatureC = Convert.ToInt32(temperature),
                Place = airTemp?.Station?.Name is null ? place : airTemp.Station.Name,
                Longitude = airTemp?.Position?[0].Longitude is null ? 0 : airTemp.Position[0].Longitude,
                Latitude = airTemp?.Position?[0].Latitude is null ? 0 : airTemp.Position[0].Latitude
            };
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(120));
            _memoryCache.Set(stationDataMemoryCacheKeyForPlace, weatherData, cacheOptions);
            return weatherData;
        }

        [HttpGet("GetStationData")]
        public async Task<IEnumerable<IStationsData>> GetAsync()
        {
            if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<IStationsData> stationDataCollection))
            {
                _logger.LogDebug("Using cached, not live, SMHI station data.");
                return stationDataCollection;
            }
            stationDataCollection = GetStationDataList(await GetStationsAsync());
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));
            _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);
            return stationDataCollection;
        }

        [HttpGet("GetStationDataForLocation")]
        public async Task<IStationsData> GetAsync(double longitude, double latitude)
        {
            // If found in cache, return cached data
            string locationCacheKey = $"long_{longitude}_lat_{latitude}";
            if (_memoryCache.TryGetValue(locationCacheKey, out StationsData stationsData))
            {
                _logger.LogDebug("Using cached, not live, SMHI station data.");
                return stationsData;
            }
            List<StationsData> sdCollection = (List<StationsData>)await GetStationDatasForLocation(longitude, latitude);
            foreach (StationsData sd in sdCollection)
                if (GetWeatherDataForStation(sd.StationsId) != null)
                {
                    stationsData = sd;
                    break;
                }
            if (stationsData == null) stationsData = sdCollection.First();
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(5));
            _memoryCache.Set(locationCacheKey, stationsData, cacheOptions);
            return stationsData;
        }

        [HttpGet("GetWeatherDatasForGeoLocation")]
        public async Task<IEnumerable<IWeatherData>> GetWeatherDatasAsync(double longitude, double latitude)
        {
            List<StationsData> sdCollection = (List<StationsData>)await GetStationDatasForLocation(longitude, latitude);
            List<WeatherData> wdCollection = new();
            StationsData stationsData;
            for (int i = 0; i < sdCollection.Count; i++)
            {
                stationsData = sdCollection[i];
                WeatherData? wd = GetWeatherDataForStation(stationsData.StationsId);
                if (wd != null) wdCollection.Add(wd);
            }
            return wdCollection;
        }

        [HttpGet("GetWeatherDataForGeoLocation")]
        public async Task<IWeatherData?> GetWeatherDataAsync(double longitude, double latitude)
        {
            List<IStationsData> sdCollection = (List<IStationsData>)await GetStationDatasForLocation(longitude, latitude);
            try
            {
                IStationsData stationsData;
                for (int i = 0; i < sdCollection.Count; i++)
                {
                    stationsData = sdCollection[i];
                    WeatherData? wd = GetWeatherDataForStation(stationsData.StationsId);
                    if (wd != null) return wd;
                }
            }
            catch (Exception ex)
            {
                DebugData = ex.Message;
                throw;
            }
            return null;
        }

        #endregion Public web methods

        #region Private methods

        private static List<IStationsData> GetStationDataList(SmhiStations? stations)
        {
            List<IStationsData> stationDatas = new();
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
                        Date = stationDateTime,
                        Longitude = smhiStation.Longitude,
                        Latitude = smhiStation.Latitude
                    });
                }
            });
            return stationDatas;
        }

        private async Task<SmhiLatestHourAirTemp?> GetAirTempAsync(string place)
        {
            await _lock.WaitAsync();
            try
            {
                // get weather stations
                if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<IStationsData>? stationDataCollection))
                    _logger.LogDebug("Using cached stationdata");
                else
                {
                    stationDataCollection = GetStationDataList(await GetStationsAsync());
                    MemoryCacheEntryOptions stationCacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));
                    _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, stationCacheOptions);
                }

                // get weather station matching place
                place = HttpUtility.UrlDecode(place).ToLowerInvariant();
                IStationsData? matchingPlace = stationDataCollection?.Find(x => x.StationsName != null && x.StationsName.ToLowerInvariant().StartsWith(place));
                if (matchingPlace is null)
                {
                    _logger.LogDebug("Could not find a station for {place}", place);
                    return null;
                }

                // build url to call to get smhi air temp for latest hour for station
                string? smhiLatestHourAirTempJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_LATEST_HOUR_AIRTEMP_URL"];
                if (string.IsNullOrWhiteSpace(smhiLatestHourAirTempJsonAddress))
                    smhiLatestHourAirTempJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL;
                string smhiLatestHourDataFromStationJsonAddress = string.Format(smhiLatestHourAirTempJsonAddress, matchingPlace.StationsId);

                // get air temp for last hour from weather station from cache if available
                if (_memoryCache.TryGetValue(smhiLatestHourDataFromStationJsonAddress, out SmhiLatestHourAirTemp? smhiLatestHourAirTemp))
                {
                    _logger.LogDebug("Using cached, not live, SMHI air temperature data.");
                    return smhiLatestHourAirTemp;
                }

                // API call; get air temp for SMHI station
                smhiLatestHourAirTemp = await _client.GetFromJsonAsync<SmhiLatestHourAirTemp>(smhiLatestHourDataFromStationJsonAddress);

                // set in memory cache
                MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(120));
                _memoryCache.Set(smhiLatestHourDataFromStationJsonAddress, smhiLatestHourAirTemp, cacheOptions);
                return smhiLatestHourAirTemp;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<SmhiStations?> GetStationsAsync()
        {
            if (_memoryCache.TryGetValue(Shared.Constants.SMHI_STATIONDATA_MEMORY_CACHE_KEY, out SmhiStations? smhiStations))
            {
                _logger.LogDebug("Using cached SMHI stations");
                return await Task.FromResult(smhiStations);
            }
            await _stationLock.WaitAsync();
            try
            {
                // double check in case another thread has completed
                if (_memoryCache.TryGetValue(Shared.Constants.SMHI_STATIONDATA_MEMORY_CACHE_KEY, out smhiStations))
                {
                    _logger.LogDebug("Using cached SMHI stations");
                    return await Task.FromResult(smhiStations);
                }
                // Read address from Application Configuration if available - otherwise use default 
                // INFO: This value is also set by action .github\workflows\WeatherZilla.AzureDeployment.yml via github to Azure (WebApp environment) during production deployment
                string? smhiStationsJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_STATIONS_URL"];
                // DEBUG: Log debug data
                DebugData = $"Tried to read application configuration key 'SMHI_URLS:SMHI_JSON_STATIONS_URL' in Azure and it returned {(string.IsNullOrWhiteSpace(smhiStationsJsonAddress) ? "nothing; using default value '" + Shared.Constants.DEFAULT_SMHI_JSON_STATIONS_URL + "'" : "'" + smhiStationsJsonAddress + "'")}.";
                _logger.LogDebug("{DebugData}", DebugData);
                if (string.IsNullOrWhiteSpace(smhiStationsJsonAddress))
                    smhiStationsJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_STATIONS_URL;
                // Call SMHI with HttpClient to get Json into class Data.SmhiStations (which should be matching their data)
                smhiStations = await _client.GetFromJsonAsync<SmhiStations>(smhiStationsJsonAddress);
                // set in memory cache
                MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(5));
                _memoryCache.Set(Shared.Constants.SMHI_STATIONDATA_MEMORY_CACHE_KEY, smhiStations, cacheOptions);
                return smhiStations;
            }
            finally
            {
                _stationLock.Release();
            }
        }

        private async Task<IEnumerable<IStationsData>> GetStationDatasForLocation(double longitude, double latitude)
        {
            // If found in cache, return cached data
            if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<IStationsData> stationDataCollection))
                _logger.LogDebug("Using cached, not live, SMHI station data.");
            else
            {
                try
                {
                    _memoryCache.TryGetValue(Shared.Constants.SMHI_STATIONDATA_MEMORY_CACHE_KEY, out SmhiStations? smhiStations);
                    stationDataCollection = GetStationDataList(smhiStations ?? await GetStationsAsync());
                    MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(5));
                    _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);

                }
                catch (Exception ex)
                {
                    DebugData = ex.Message;
                    throw;
                }
            }
            // Get stationdata list sorted closest to user longitude and latitude
            GeoCoordinate.NetStandard2.GeoCoordinate? userCoordinate = new(latitude, longitude);
            IEnumerable<GeoCoordinate.NetStandard2.GeoCoordinate> nearestStationCoordinates = stationDataCollection.Select(x => new GeoCoordinate.NetStandard2.GeoCoordinate(x.Latitude, x.Longitude))
                                   .Where(x => x != null)
                                   .OrderBy(x => x.GetDistanceTo(userCoordinate)).Distinct().Take(10);
            List<IStationsData> stationsDatas = new();
            IEnumerable<IStationsData>? tempStationsDatas = null;
            if (nearestStationCoordinates != null)
                foreach (GeoCoordinate.NetStandard2.GeoCoordinate nearestStationCoordinate in nearestStationCoordinates)
                {
                    tempStationsDatas = (IEnumerable<IStationsData>?)stationDataCollection.Where(x => x.Longitude.Equals(nearestStationCoordinate.Longitude) &&
                        x.Latitude.Equals(nearestStationCoordinate.Latitude) && !string.IsNullOrEmpty(x.StationsName)).DistinctBy(y => y.StationsName).Take(10);
                    if (tempStationsDatas != null) stationsDatas.AddRange(tempStationsDatas);
                }
            return stationsDatas;
        }

        private WeatherData? GetWeatherDataForStation(int stationID)
        {
            string? smhiLatestHourAirTempJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_LATEST_HOUR_AIRTEMP_URL"];
            if (string.IsNullOrWhiteSpace(smhiLatestHourAirTempJsonAddress))
                smhiLatestHourAirTempJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL;
            smhiLatestHourAirTempJsonAddress = string.Format(smhiLatestHourAirTempJsonAddress, stationID);
            try
            {
                // If found in cache, return cached data
                if (_memoryCache.TryGetValue(smhiLatestHourAirTempJsonAddress, out WeatherData weatherData))
                {
                    _logger.LogDebug("Using cached, not live, SMHI station airtemp data for station ID {stationID}.", stationID);
                    return weatherData;
                }
                // API call; get temperature in Celsius for SMHI station with identifier smhiStationID)
                SmhiLatestHourAirTemp? stationTemp = _client.GetFromJsonAsync<SmhiLatestHourAirTemp>(smhiLatestHourAirTempJsonAddress).Result;
                if (stationTemp is null || stationTemp?.ValueData is null) return null;

                string? temperature = stationTemp?.ValueData?[0]?.RoundedValue;
                DateTime weatherDateTime = Data.SmhiDateHelper.GetDateTimeFromSmhiDate(stationTemp?.ValueData?[0]?.Date);
                weatherData = new WeatherData()
                {
                    Date = weatherDateTime,
                    TemperatureC = Convert.ToInt32(temperature),
                    Place = stationTemp?.Station?.Name,
                    Longitude = stationTemp?.Position?[0].Longitude is null ? 0 : stationTemp.Position[0].Longitude,
                    Latitude = stationTemp?.Position?[0].Latitude is null ? 0 : stationTemp.Position[0].Latitude
                };
                MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(120));
                _memoryCache.Set(smhiLatestHourAirTempJsonAddress, weatherData, cacheOptions);
                return weatherData;
            }
            catch (Exception ex)
            {
                DebugData = ex.ToString();
                _logger.LogError("Could not get latest hour air temp from url {smhiLatestHourAirTempJsonAddress}, exception: {DebugData}", smhiLatestHourAirTempJsonAddress, DebugData);
                return null;
            }
        }

        #endregion Private methods
    }
}