using System.Globalization;
using System.Text.Json.Serialization;

namespace WeatherZilla.Data
{
    // SmhiLatestHourAirTemp myDeserializedClass = JsonConvert.DeserializeObject<SmhiLatestHourAirTemp>(myJsonResponse);
    // SmhiLatestHourAirTemp myDeserializedClass = JsonSerializer.Deserialize<SmhiLatestHourAirTemp>(myJsonResponse);
    public class Link
    {
        [JsonPropertyName("rel")]
        public string? Rel { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("href")]
        public string? Href { get; set; }
    }

    public class Parameter
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    public class Period
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("from")]
        public long From { get; set; }

        [JsonPropertyName("to")]
        public long To { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("sampling")]
        public string? Sampling { get; set; }
    }

    public class Position
    {
        [JsonPropertyName("from")]
        public long From { get; set; }

        [JsonPropertyName("to")]
        public object? To { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    public class SmhiLatestHourAirTemp
    {
        [JsonPropertyName("value")]
        public List<ValueData>? ValueData { get; set; }

        [JsonPropertyName("updated")]
        public long Updated { get; set; }

        [JsonPropertyName("parameter")]
        public Parameter? Parameter { get; set; }

        [JsonPropertyName("station")]
        public Station? Station { get; set; }

        [JsonPropertyName("period")]
        public Period? Period { get; set; }

        [JsonPropertyName("position")]
        public List<Position>? Position { get; set; }

        [JsonPropertyName("link")]
        public List<Link>? Link { get; set; }
    }

    public class Station
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("ownerCategory")]
        public string? OwnerCategory { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }

    public class ValueData
    {
        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        /// <summary>
        /// Temperature rounded to nearest whole degree C
        /// </summary>
        public string RoundedValue
        {
            get
            {
                if (Value == null) return "";
                NumberFormatInfo provider = new() { CurrencyDecimalSeparator = "." };
                double dblValue = Convert.ToDouble(Value, provider);
                string roundedValue = Math.Round(dblValue, 0).ToString();
                return roundedValue;
            }
            private set { }
        }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }
    }

}
