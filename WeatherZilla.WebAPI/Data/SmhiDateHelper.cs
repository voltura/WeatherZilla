namespace WeatherZilla.WebAPI.Data
{
    public class SmhiDateHelper
    {
        public static DateTime GetDateTimeFromSmhiDate(object? smhiMillisecondsFrom1970Utc)
        {
            DateTime dateTime = DateTime.MinValue;
            if (smhiMillisecondsFrom1970Utc != null && double.TryParse(smhiMillisecondsFrom1970Utc.ToString(), out double dMilliSecs))
            {
                dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                double secondsFromSeventies = ((double)dMilliSecs > 0) ? (double)dMilliSecs / 1000 : 0;
                dateTime = dateTime.AddSeconds(secondsFromSeventies).ToLocalTime();
            }
            return dateTime;
        }
    }
}
