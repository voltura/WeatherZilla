using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace WeatherZilla.WebApp.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public string? ErrorInfoToUser { get; set; }
        public string? Path { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        private readonly ILogger<ErrorModel> _logger;
        private readonly IWebHostEnvironment _env;

        public bool IsDevelopment => _env != null && _env.IsDevelopment();

        public ErrorModel(ILogger<ErrorModel> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public ActionResult OnGet()
        {
            HandleError();
            return Page();
        }

        public ActionResult OnPost()
        {
            HandleError();
            return Page();
        }

        private void HandleError()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier; 
            ExceptionHandlerFeature? exceptionHandlerFeature = (ExceptionHandlerFeature?)HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            Path = exceptionHandlerFeature?.Path;
            ErrorInfoToUser = "Something went wrong, please try again.";
            _logger.LogError("Error occurred: {exceptionHandlerFeature}", exceptionHandlerFeature);
            if (Path != null && Path.ToLowerInvariant().Contains("login"))
            {
                ErrorInfoToUser = "Failed login, database is potentially starting up, please try again.";
            }
            else if (Path != null && Path.ToLowerInvariant().Contains("register"))
            {
                ErrorInfoToUser = "Failed to register, database is potentially starting up, please try again.";
            }
            else if (Path != null && Path.ToLowerInvariant().Contains("account"))
            {
                ErrorInfoToUser = "Failed to authenticate, database is potentially starting up, please try again.";
            }
        }
    }
}