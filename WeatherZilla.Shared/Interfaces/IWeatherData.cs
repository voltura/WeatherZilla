namespace WeatherZilla.Shared.Interfaces
{
    public interface IWeatherData
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }

        public string? Place { get; set; }
        public string FormattedPlace
        {
            get
            {
                string actualPlace = string.IsNullOrWhiteSpace(Place) ? "Unknown" : Place;
                actualPlace = (actualPlace.Contains('-') ? actualPlace.Split(new char[] { '-' })[0] : actualPlace).Trim();
                actualPlace = actualPlace.EndsWith(" A") ? actualPlace[..^2] : actualPlace;
                return actualPlace;
            }
            private set { }
        }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

    }
}