using System.Text.Json.Serialization;

namespace WeatherZilla.WebAPI.SmhiStationData
{

    // NOTES: Classes shall match json from smhi on this url https://opendata-download-metobs.smhi.se/api/version/latest/parameter/1.json
    //        JSON instance converted online here to create these classes: https://json2csharp.com/
    //        Example usage: SmhiStations myDeserializedClass = JsonSerializer.Deserialize<SmhiStations>(myJsonResponse);
    public class Link
    {
        [JsonPropertyName("rel")]
        public string? Rel { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("href")]
        public string? Href { get; set; }
    }

    public class SmhiStations
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("updated")]
        public long Updated { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("valueType")]
        public string? ValueType { get; set; }

        [JsonPropertyName("link")]
        public List<Link>? Link { get; set; }

        [JsonPropertyName("stationSet")]
        public List<StationSet>? StationSet { get; set; }

        [JsonPropertyName("station")]
        public List<Station>? Station { get; set; }
    }

    public class Station
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("ownerCategory")]
        public string? OwnerCategory { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("from")]
        public object? From { get; set; }

        [JsonPropertyName("to")]
        public object? To { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("updated")]
        public object? Updated { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("link")]
        public List<Link>? Link { get; set; }
    }

    public class StationSet
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("updated")]
        public long Updated { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("link")]
        public List<Link>? Link { get; set; }
    }

}
