namespace WeatherZilla.WebApp.Data;

public class FavoritePlace
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Place { get; set; } = string.Empty;
}
