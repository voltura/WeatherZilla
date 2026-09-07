using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeatherZilla.Shared.Data;
using WeatherZilla.WebAPI.Controllers;

using var cache = new MemoryCache(new MemoryCacheOptions());
cache.Set(WeatherZilla.Shared.Constants.STATIONDATA_MEMORY_CACHE_KEY, new List<StationsData>());
var logger = new CaptureLogger<WeatherDataController>();
var controller = new WeatherDataController(logger, new ConfigurationBuilder().Build(), cache);
foreach (string place in new[] { "Umeå", "missing\r\nforged", "%0d%0aforged", "missing\u0085\u2028\u2029forged" })
{
    logger.Lines.Clear();
    var first = await controller.GetAsync(place);
    var cached = await controller.GetAsync(place);
    if (first?.Place != place || !ReferenceEquals(first, cached)) throw new Exception("Lookup/cache behavior changed");
    if (logger.Lines.Any(line => line.IndexOfAny(new[] { '\r', '\n', '\u0085', '\u2028', '\u2029' }) >= 0))
        throw new Exception("Untrusted place split a log entry");
    if (!logger.Lines.Any(line => line.Contains("Could not find")) || !logger.Lines.Any(line => line.Contains("Using cached SMHI")))
        throw new Exception("Expected both reported log paths");
}
Console.WriteLine("PASS raw/decoded line separators cannot forge logs; ordinary place and caching behavior preserved");

sealed class CaptureLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
}
