namespace WeatherZilla.Shared.Interfaces
{
    public interface IStationsData
    {
        public DateTime Date { get; set; }
        public int StationsId { get; set; }
        public string? StationsName { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }

    }
}
