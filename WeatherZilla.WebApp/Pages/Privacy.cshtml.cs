using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.WebApp.Pages
{
    public class PrivacyModel : PageModel
    {
        private readonly ILogger<PrivacyModel> _logger;

        public PrivacyModel(ILogger<PrivacyModel> logger)
        {
            _logger = logger;
            _logger.LogTrace("Created PrivacyModel");
        }

        public void OnGet() { }
    }
}