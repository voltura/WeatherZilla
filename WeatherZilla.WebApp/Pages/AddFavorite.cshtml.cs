using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WeatherZilla.Shared.Data;
using WeatherZilla.WebApp.Data;

namespace WeatherZilla.WebApp.Pages;

[Authorize]
public class AddPlaceModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AddPlaceModel> _logger;

    public AddPlaceModel(ApplicationDbContext db, IHttpClientFactory clients, IConfiguration configuration, ILogger<AddPlaceModel> logger)
    {
        _db = db;
        _client = clients.CreateClient("Stations");
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty, Required, StringLength(128, MinimumLength = 2)]
    public string Place { get; set; } = string.Empty;
    public List<StationsData> Matches { get; private set; } = new();
    public List<FavoritePlace> Favorites { get; private set; } = new();
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId) return Challenge();
        await LoadFavorites(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostSearchAsync()
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId) return Challenge();
        await LoadFavorites(userId);
        Place = Place?.Trim() ?? string.Empty;
        if (Place.Length < 2) ModelState.AddModelError(nameof(Place), "Enter at least two characters.");
        if (!ModelState.IsValid) return Page();
        var stations = await GetStations();
        if (stations is null) return Page();
        Matches = stations.Where(s => s.StationsName?.Contains(Place, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(s => s.StationsName).Take(30).ToList();
        if (Matches.Count == 0) StatusMessage = "No matching places found.";
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(int stationId)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId) return Challenge();
        // Resolve the posted identifier against the provider; never accept a posted owner or place name.
        ModelState.Clear();
        var stations = await GetStations();
        var station = stations?.FirstOrDefault(s => s.StationsId == stationId);
        if (string.IsNullOrWhiteSpace(station?.StationsName) || station.StationsName.Length > 128)
        {
            if (stations is not null) ModelState.AddModelError(string.Empty, "Choose a place from the search results.");
            await LoadFavorites(userId);
            return Page();
        }
        if (await _db.Favorites.AnyAsync(f => f.UserId == userId && f.Place == station.StationsName))
        {
            StatusMessage = "That place is already in your favorites.";
            return RedirectToPage();
        }
        _db.Favorites.Add(new FavoritePlace { UserId = userId, Place = station.StationsName });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        { StatusMessage = "That place is already in your favorites."; return RedirectToPage(); }
        StatusMessage = $"Added {station.StationsName} to your favorites.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId) return Challenge();
        var favorite = await _db.Favorites.SingleOrDefaultAsync(f => f.Id == id && f.UserId == userId);
        if (favorite is null) return NotFound();
        _db.Favorites.Remove(favorite);
        await _db.SaveChangesAsync();
        StatusMessage = "Favorite removed.";
        return RedirectToPage();
    }

    private async Task LoadFavorites(string userId) => Favorites = await _db.Favorites.AsNoTracking()
        .Where(f => f.UserId == userId).OrderBy(f => f.Place).ToListAsync();

    private async Task<List<StationsData>?> GetStations()
    {
        try
        {
            var url = _configuration["WEATHERZILLA_WEBAPI_URLS:STATIONDATA_URL"];
            return await _client.GetFromJsonAsync<List<StationsData>>(string.IsNullOrWhiteSpace(url)
                ? WeatherZilla.Shared.Constants.DEFAULT_STATIONDATA_URL : url) ?? new();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Could not load weather stations");
            ModelState.AddModelError(string.Empty, "Places could not be loaded. Please try again.");
            return null;
        }
    }
}
