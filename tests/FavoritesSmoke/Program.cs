using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherZilla.WebApp.Data;
using WeatherZilla.WebApp.Pages;

var database = "WeatherZillaSmoke_" + Guid.NewGuid().ToString("N");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new ApplicationDbContext(options);
try
{
    // Exercise a fresh database as well as the manually created legacy table.
    var freshOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database}_Fresh;Trusted_Connection=True;TrustServerCertificate=True").Options;
    await using (var fresh = new ApplicationDbContext(freshOptions))
    {
        try { await fresh.Database.MigrateAsync(); Check(!await fresh.Favorites.AnyAsync(), "fresh database migrations succeed"); }
        finally { await fresh.Database.EnsureDeletedAsync(); }
    }
    // First create the old EF schema and manually defined favorites table to test real upgrades.
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20220514205503_InitialCreate");
    db.Users.AddRange(new IdentityUser { Id = "alice", UserName = "alice" }, new IdentityUser { Id = "bob", UserName = "bob" });
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AspNetFavorites (Id int NOT NULL PRIMARY KEY, UserId nvarchar(450) NOT NULL, Place nvarchar(128) NOT NULL); INSERT dbo.AspNetFavorites VALUES (42, 'bob', N'Existing favorite');");
    await db.Database.MigrateAsync();
    Check(await db.Favorites.AnyAsync(f => f.Id == 42), "migration preserves existing favorite");
    var page = Page("alice");
    page.Place = "Stock";
    await page.OnPostSearchAsync();
    Check(page.Matches.Count == 1, "search matches provider places");
    await page.OnPostAddAsync(1);
    var saved = await db.Favorites.SingleAsync(f => f.UserId == "alice");
    Check(saved.Place == "Stockholm" && saved.Id > 42, "add uses provider name and generated identifier");
    await page.OnPostAddAsync(1);
    Check(await db.Favorites.CountAsync(f => f.UserId == "alice") == 1, "duplicate is not added");
    Check(await Page("bob").OnPostRemoveAsync(saved.Id) is NotFoundResult, "cross-user delete is rejected");
    await Page("alice").OnPostRemoveAsync(saved.Id);
    Check(!await db.Favorites.AnyAsync(f => f.Id == saved.Id), "owner can remove favorite");
    var bob = Page("bob"); await bob.OnGetAsync();
    Check(bob.Favorites.Count == 1 && bob.Favorites[0].Id == 42, "list is scoped to owner");
    Check(await Page(null).OnPostAddAsync(1) is ChallengeResult, "missing identity is rejected");
    var invalid = Page("alice"); await invalid.OnPostAddAsync(999);
    Check(!invalid.ModelState.IsValid && await db.Favorites.CountAsync() == 1, "unknown station is rejected");
    await db.Database.MigrateAsync();
    Check(await db.Favorites.CountAsync() == 1, "repeated migration preserves data");
}
finally { await db.Database.EnsureDeletedAsync(); }

AddPlaceModel Page(string? user)
{
    var context = new DefaultHttpContext();
    if (user != null) context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user) }, "test"));
    var page = new AddPlaceModel(db, new Clients(), new ConfigurationBuilder().Build(), NullLogger<AddPlaceModel>.Instance);
    page.PageContext = new PageContext { HttpContext = context };
    page.TempData = new TempDataDictionary(context, new TempProvider());
    return page;
}
static void Check(bool condition, string description) { if (!condition) throw new Exception(description); Console.WriteLine("PASS " + description); }
sealed class Clients : IHttpClientFactory { public HttpClient CreateClient(string name) => new(new Stations()); }
sealed class Stations : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"stationsId\":1,\"stationsName\":\"Stockholm\"},{\"stationsId\":2,\"stationsName\":\"Umeå\"}]", System.Text.Encoding.UTF8, "application/json") });
}
sealed class TempProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
