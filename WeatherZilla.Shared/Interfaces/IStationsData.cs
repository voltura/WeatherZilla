namespace WeatherZilla.Shared.Interfaces
{
    public interface IStationsData
    {
        public DateTime Date { get; set; }

        public int StationsId { get; set; }

        public string? StationsName { get; set; }

        public bool? Active { get; set; }

    }
}