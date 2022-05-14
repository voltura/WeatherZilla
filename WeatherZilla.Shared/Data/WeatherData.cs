using WeatherZilla.Shared.Interfaces;

namespace WeatherZilla.Shared.Data
{
    public class WeatherData : IWeatherData
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }

        public string? Place { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

    }
}