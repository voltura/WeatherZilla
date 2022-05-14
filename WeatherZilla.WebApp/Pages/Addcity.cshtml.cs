using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.WebApp.Pages
{
    public class AddcityModel : PageModel
    {
        public string CityName { get; set; }
        private readonly IConfiguration _configuration;

        public AddcityModel(IConfiguration configuration)
        {
            CityName = "Lycksele";
            _configuration = configuration;
        }

        public void OnGet()
        {
            CityName = "Lycksele";
        }
    }
}