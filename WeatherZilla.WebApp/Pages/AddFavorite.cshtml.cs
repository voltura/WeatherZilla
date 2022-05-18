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
                return Status switch
                {
                    STATUS.Searching => $"Searching for {Place}...",
                    STATUS.SearchSuccess => $"Found {Place}",
                    STATUS.NoSearchResult => $"Didn't find {Place}",
                    STATUS.SearchFailure => $"Unable to perform search for {Place}",
                    STATUS.NoUser => "Not logged in",
                    STATUS.ReadyForSearch => $"Input {Place} accepted",
                    /*  STATUS.NoSearchPerformed:
                        STATUS.MissingInput:
                        STATUS.SearchAborted:
                        STATUS.InvalidInput:  */
                    _ => "Please enter a place",
                };
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

            bool validUserInput = userInput.All(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c) || c == '-');
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