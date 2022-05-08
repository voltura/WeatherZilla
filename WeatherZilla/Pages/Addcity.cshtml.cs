using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.Pages
{
    public class AddcityModel : PageModel
    {
        public string CityName { get; set; }

        public AddcityModel()
        {
            CityName = "Lycksele";
        }

        public void OnGet()
        {
            CityName = "Lycksele";
        }
    }
}