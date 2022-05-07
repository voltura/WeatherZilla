using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.Pages
{
    public class AddcityModel : PageModel
    {
        public string cityName { get; set; }
        public void OnGet()
        {
            cityName = "Lycksele";
        }
    }
}