using WeatherZilla.Shared.Interfaces;

namespace WeatherZilla.Shared.Data
{
    public class StationsData : IStationsData
    {
        public DateTime Date { get; set; }
        public int StationsId { get; set; }
        public string? StationsName { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }

    }
}
