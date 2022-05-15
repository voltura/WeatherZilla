using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeatherZilla.WebApp.Pages
{
    public class AddcityModel : PageModel
    {
        public string CityName { get; set; }
        public string Place { get; set; }
        private readonly IConfiguration _configuration;

        public AddcityModel(IConfiguration configuration)
        {
            CityName = "Lycksele";
            _configuration = configuration;
        }

        public void OnGet()
        {
            CityName = "Lycksele";
            if (String.IsNullOrEmpty(Place))
            {
                Place = "No search yet";
            }
        }
        public void OnPost()
        {
            var placeSearchedFor = Request.Form["place"];
            Place = placeSearchedFor;
            // TODO: Search weather provider for placeSearchedFor and add a valid place to database
        }
    }
}