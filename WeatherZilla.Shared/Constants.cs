namespace WeatherZilla.Shared
{
    public static class Constants
    {
        #region WeatherZilla WebAPI addresses

        public static readonly string DEFAULT_WEATHERDATA_FOR_PLACE_URL = "https://weatherzilla.azurewebsites.net/api/WeatherData/GetWeatherData?place=";
        public static readonly string DEFAULT_STATIONDATA_URL = "https://weatherzilla.azurewebsites.net/api/WeatherData/GetStationData";
        public static readonly string DEFAULT_WEATHERDATA_FOR_GEOLOCATION_URL = $"https://weatherzilla.azurewebsites.net/api/WeatherData/GetWeatherDataForGeoLocation?longitude={0}&latitude={1}";

        #endregion WeatherZilla WebAPI addresses

        #region SMHI JSON addresses

        public static readonly string DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL = "https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1/station/{0}/period/latest-hour/data.json";
        public static readonly string DEFAULT_SMHI_JSON_STATIONS_URL = "https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1.json";

        #endregion SMHI JSON addresses

        #region Memory cache keys

        public static readonly string STATIONDATA_MEMORY_CACHE_KEY = "STATIONDATA_MEMORY_CACHE_KEY";
        public static readonly string SMHI_STATIONDATA_MEMORY_CACHE_KEY = "SMHI_STATIONDATA_MEMORY_CACHE_KEY";
        public static readonly string AIRTEMP_CACHE_KEY = "AIRTEMP_CACHE_KEY";

        #endregion Memory cache keys
    }
}
