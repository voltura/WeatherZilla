#region Using statements

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
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
            return await GetWeatherDataForPlace(place);
        }

        [HttpGet("GetStationData")]
        public async Task<IEnumerable<IStationsData>> GetAsync()
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

        [HttpGet("GetStationDataForLocation")]
        public async Task<IStationsData> GetAsync(double longitude, double latitude)
        {
            return await GetStationDataForLocation(longitude, latitude);
        }

        [HttpGet("GetWeatherDatasForGeoLocation")]
        public async Task<IEnumerable<IWeatherData>> GetWeatherDatasAsync(double longitude, double latitude)
        {
            List<StationsData> sdCollection = (List<StationsData>)await GetStationDatasForLocation(longitude, latitude);
            List<WeatherData> wdCollection = new();
            
            for (int i = 0; i < sdCollection.Count; i++)
            {
                var stationData = sdCollection[i];
                WeatherData? wd = GetWeatherDataForStation(stationData.StationsId);
                if (wd != null)
                    wdCollection.Add(wd);
            }
            return wdCollection;
        }

        [HttpGet("GetWeatherDataForGeoLocation")]
        public async Task<IWeatherData?> GetWeatherDataAsync(double longitude, double latitude)
        {
            List<StationsData> sdCollection = (List<StationsData>)await GetStationDatasForLocation(longitude, latitude);

            for (int i = 0; i < sdCollection.Count; i++)
            {
                var stationData = sdCollection[i];
                WeatherData? wd = GetWeatherDataForStation(stationData.StationsId);
                if (wd != null)
                    return wd;
            }
            return null;
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
                        Date = stationDateTime,
                        Longitude = smhiStation.Longitude,
                        Latitude = smhiStation.Latitude
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

        private async Task<IEnumerable<IStationsData>> GetStationDatasForLocation(double longitude, double latitude)
        {
            // If found in cache, return cached data
            if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<StationsData> stationDataCollection))
            {
                _logger.LogDebug("Using cached, not live, SMHI station data.");
            }
            else
            {
                stationDataCollection = GetStationDataList(_stations ?? await GetStationsAsync());
                // Set cache options
                MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));
                // Set object in cache
                _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);
            }
            // Get stationdata list sorted closest to user longitude and latitude
            GeoCoordinate.NetStandard2.GeoCoordinate? userCoordinate = new(latitude, longitude);
            IEnumerable<GeoCoordinate.NetStandard2.GeoCoordinate> nearestStationCoordinates = stationDataCollection.Select(x => new GeoCoordinate.NetStandard2.GeoCoordinate(x.Latitude, x.Longitude))
                                   .Where(x => x != null)
                                   .OrderBy(x => x.GetDistanceTo(userCoordinate)).Distinct().Take(10);
            List<StationsData> stationsDatas = new();
            StationsData? stationsData = null;
            IEnumerable<StationsData>? tempStationsDatas = null;
            if (nearestStationCoordinates != null)
            {
                foreach (GeoCoordinate.NetStandard2.GeoCoordinate nearestStationCoordinate in nearestStationCoordinates)
                {
                    tempStationsDatas = (IEnumerable<StationsData>?)stationDataCollection.Where(x => x.Longitude.Equals(nearestStationCoordinate.Longitude) &&
                        x.Latitude.Equals(nearestStationCoordinate.Latitude) && !string.IsNullOrEmpty(x.StationsName)).DistinctBy(y => y.StationsName).Take(10);
                    if (tempStationsDatas != null)
                    {
                        stationsDatas.AddRange(tempStationsDatas);
                    }
                }
            }

            // If no match, just try to get a value
            if (stationsDatas.Count < 1 || (stationsDatas.Count == 1 && string.IsNullOrEmpty(stationsDatas[0].StationsName)))
            {
                stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 2).Equals(Math.Round(longitude, 2)) && Math.Round(x.Latitude, 2).Equals(Math.Round(latitude, 2))).LastOrDefault(new StationsData());
                if (string.IsNullOrEmpty(stationsData?.StationsName))
                {
                    stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 1).Equals(Math.Round(longitude, 1)) && Math.Round(x.Latitude, 1).Equals(Math.Round(latitude, 1))).LastOrDefault(new StationsData());
                }
                if (string.IsNullOrEmpty(stationsData?.StationsName))
                {
                    stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 0).Equals(Math.Round(longitude, 0)) && Math.Round(x.Latitude, 0).Equals(Math.Round(latitude, 0))).LastOrDefault(new StationsData());
                }
                if (string.IsNullOrEmpty(stationsData?.StationsName))
                {
                    stationsData = new() { StationsName = "Lycksele" };
                }
                stationsDatas.Add(stationsData);
            }
            return stationsDatas;
        }

        private async Task<IStationsData> GetStationDataForLocation(double longitude, double latitude)
        {

            // TODO: Do not rely on this slow function that handles all stations with linq etc. also implement the memory cache for the value
            List<StationsData> sdCollection = (List<StationsData>)await GetStationDatasForLocation(longitude, latitude);
            for (int i = 0; i < sdCollection.Count; i++)
            {
                var stationData = sdCollection[i];

                if (GetWeatherDataForStation(stationData.StationsId) != null)
                    return stationData;
            }
            return sdCollection.ToArray()[0];

            //// If found in cache, return cached data
            //if (_memoryCache.TryGetValue(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, out List<StationsData> stationDataCollection))
            //{
            //    _logger.LogDebug("Using cached, not live, SMHI station data.");
            //}
            //else
            //{
            //    stationDataCollection = GetStationDataList(_stations ?? await GetStationsAsync());
            //    // Set cache options
            //    MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(10));
            //    // Set object in cache
            //    _memoryCache.Set(Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, stationDataCollection, cacheOptions);
            //}
            //// Try to get stationdata closest to user longitude and latitude
            //// TODO: Do the linq selection on already filtered list where temperature is available
            //GeoCoordinate.NetStandard2.GeoCoordinate? userCoordinate = new(latitude, longitude);
            //GeoCoordinate.NetStandard2.GeoCoordinate? nearestStationCoordinate = stationDataCollection.Select(x => new GeoCoordinate.NetStandard2.GeoCoordinate(x.Latitude, x.Longitude))
            //                       .Where(x => x != null)
            //                       .OrderBy(x => x.GetDistanceTo(userCoordinate))
            //                       .FirstOrDefault();
            //StationsData stationsData = new();
            //if (nearestStationCoordinate != null) 
            //{
            //    stationsData = stationDataCollection.Where(x => x.Longitude.Equals(nearestStationCoordinate.Longitude) &&
            //    x.Latitude.Equals(nearestStationCoordinate.Latitude) && !string.IsNullOrEmpty(x.StationsName)).FirstOrDefault(new StationsData());
            //}
            //// If no match, just try to get a value
            //if (string.IsNullOrEmpty(stationsData.StationsName))
            //{
            //    stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 2).Equals(Math.Round(longitude, 2)) && Math.Round(x.Latitude, 2).Equals(Math.Round(latitude, 2))).LastOrDefault(new StationsData());
            //}
            //if (string.IsNullOrEmpty(stationsData.StationsName))
            //{
            //    stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 1).Equals(Math.Round(longitude, 1)) && Math.Round(x.Latitude, 1).Equals(Math.Round(latitude, 1))).LastOrDefault(new StationsData());
            //}
            //if (string.IsNullOrEmpty(stationsData.StationsName))
            //{
            //    stationsData = stationDataCollection.Where(x => Math.Round(x.Longitude, 0).Equals(Math.Round(longitude, 0)) && Math.Round(x.Latitude, 0).Equals(Math.Round(latitude, 0))).LastOrDefault(new StationsData());
            //}
            //if (string.IsNullOrEmpty(stationsData.StationsName))
            //{
            //    stationsData.StationsName = "Lycksele"; // DEBUG: Default to Lycksele, because why not? Change this behavior to something else
            //}
            //return stationsData;
        }

        private WeatherData? GetWeatherDataForStation(int stationID)
        {
            string? smhiLatestHourAirTempJsonAddress = _configuration?["SMHI_URLS:SMHI_JSON_LATEST_HOUR_AIRTEMP_URL"];
            if (string.IsNullOrWhiteSpace(smhiLatestHourAirTempJsonAddress))
            {
                smhiLatestHourAirTempJsonAddress = Shared.Constants.DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL;
            }
            // API call; get temperature in Celsius for SMHI station with identifier smhiStationID)
            string smhiLatestHourDataFromStationJsonAddress = string.Format(smhiLatestHourAirTempJsonAddress, stationID);
            try
            {
                SmhiLatestHourAirTemp? stationTemp = _client.GetFromJsonAsync<SmhiLatestHourAirTemp>(smhiLatestHourDataFromStationJsonAddress).Result;
                //SmhiLatestHourAirTemp? stationTemp = await _client.GetFromJsonAsync<SmhiLatestHourAirTemp>(smhiLatestHourDataFromStationJsonAddress).Result;
                if (stationTemp is null || stationTemp?.ValueData is null)
                    return null;

                string? temperature = stationTemp?.ValueData?[0]?.RoundedValue;
                DateTime weatherDateTime = Data.SmhiDateHelper.GetDateTimeFromSmhiDate(stationTemp?.ValueData?[0]?.Date);
                return new WeatherData()
                {
                    Date = weatherDateTime,
                    TemperatureC = Convert.ToInt32(temperature),
                    Place = stationTemp?.Station?.Name,
                    Longitude = stationTemp?.Position?[0].Longitude is null ? 0 : stationTemp.Position[0].Longitude,
                    Latitude = stationTemp?.Position?[0].Latitude is null ? 0 : stationTemp.Position[0].Latitude
                };

            }
            catch (Exception ex)
            {
                DebugData = ex.ToString();
                _logger.LogError("Could not get latest hour air temp from url {smhiLatestHourDataFromStationJsonAddress}", smhiLatestHourDataFromStationJsonAddress);
                return null;
            }
        }

        private async Task<IEnumerable<WeatherData>> GetWeatherDataForPlace(string place)
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

        #endregion Private methods
    }
}