namespace WeatherZilla.Shared
{
    public static class Constants
    {
        #region WeatherZilla WebAPI addresses

        public static readonly string DEFAULT_WEATHERDATA_FOR_PLACE_URL = "https://weatherzilla.azurewebsites.net/api/WeatherData/GetWeatherData?place=";
        public static readonly string DEFAULT_STATIONDATA_URL = "https://weatherzilla.azurewebsites.net/api/WeatherData/GetStationData";

        #endregion WeatherZilla WebAPI addresses

        #region SMHI JSON addresses

        public static readonly string DEFAULT_SMHI_JSON_LATEST_HOUR_AIRTEMP_URL = "https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1/station/{0}/period/latest-hour/data.json";
        public static readonly string DEFAULT_SMHI_JSON_STATIONS_URL = "https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1.json";

        #endregion SMHI JSON addresses
    }
}
