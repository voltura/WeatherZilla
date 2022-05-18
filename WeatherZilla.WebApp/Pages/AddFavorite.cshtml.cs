using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace WeatherZilla.WebApp.Pages
{
    public class AddPlaceModel : PageModel
    {
        #region Public properties

        public string StatusMessage
        {
            get
            {
                switch (Status)
                {
                    case STATUS.Searching:
                        return $"Searching for {Place}...";
                    case STATUS.SearchSuccess:
                        return $"Found {Place}";
                    case STATUS.NoSearchResult:
                        return $"Didn't find {Place}";
                    case STATUS.SearchFailure:
                        return $"Unable to perform search for {Place}";
                    case STATUS.NoUser:
                        return "Not logged in";
                    case STATUS.ReadyForSearch:
                        return $"Input {Place} accepted";
                    /*case STATUS.NoSearchPerformed:
                    case STATUS.MissingInput:
                    case STATUS.SearchAborted:
                    case STATUS.InvalidInput:*/
                    default:
                        return "Please enter a place";
                }
            }
            private set { }
        }
        public string? Place { get; set; }
        public string? UserName { get; set; }
        public STATUS Status { get; private set; }

        #endregion Public properties

        private readonly IConfiguration _configuration;

        public enum STATUS { NoSearchPerformed, ReadyForSearch, Searching, SearchSuccess, NoSearchResult, MissingInput, InvalidInput, SearchFailure, SearchAborted, NoUser };

        public AddPlaceModel(IConfiguration configuration)
        {
            _configuration = configuration;
            Status = STATUS.NoSearchPerformed;
        }

        public void OnGet()
        {
            UserName = GetLoggedInUser();
        }

        public void OnPost()
        {
            GetUserInput();

            // TODO: Search weather provider for 'Place' and add a valid place to database
            // Suggestion; Maybe we want to search when user writes, let's say after 3 characters and onwards
            // Then the Post or Submit button press is used for only add to db?
        }

        #region Private methods

        private string GetLoggedInUser()
        {
            string? loggedInUser = User?.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(loggedInUser))
            {
                // TODO: Handle error scenario correctly. User need to be logged in for this page...
                Status = STATUS.NoUser;
                return "";
            }
            return loggedInUser;
        }

        private void GetUserInput()
        {
            string userInput = Request.Form["Place"];

            if (string.IsNullOrWhiteSpace(userInput))
            {
                Status = STATUS.MissingInput;
                return;
            }

            bool validUserInput = userInput.All(c => Char.IsLetterOrDigit(c));
            if (!validUserInput)
            {
                Status = STATUS.InvalidInput;
                return;
            }

            Status = STATUS.ReadyForSearch;
            Place = userInput;
        }

        #endregion Private methods
    }
}