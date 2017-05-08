using System;

namespace Search.Dialogs.UserInteraction
{
    [Serializable]
    public class Prompts
    {
        // Prompts
        public string AddedToListPrompt = "{0} was added to your list.";
        public string AddKeywordPrompt = "Type a keyword phrase to search for.";
        public string AddOrRemoveKeywordPrompt = "Type a keyword phrase to search for, or select what you would like to remove.";
        public string FacetPrompt = "What would you like to refine by?";
        public string FacetValuePrompt = "What value for {0} would you like to filter by?";
        public string FilterPrompt = "Enter a filter for {0} like \"no more than 4\".";
        public string InitialPrompt = "Please describe in your own words what you would like to find?";
        public string ListPrompt = "Here is what you have selected so far.";
        public string NoResultsPrompt = "Your search found no results so I undid your last change.  You can refine again.";
        public string NotAddedPrompt = "You have not added anything yet.";
        public string NotUnderstoodPrompt = "I did not understand what you said.";
        public string NoValuesPrompt = "There are no values to filter by for {0}.";
        public string RefinePrompt = "Refine your search or select an operation.";
        public string RemovedFromListPrompt = "{0} was removed from your list.";
        public string UnknownItemPrompt = "That is not an item in the current results.";

        // Buttons
        public Button Add = new Button("Add to List", "ADD:{0}");
        public Button Finished = new Button("Finished");
        public Button Keyword = new Button("Keyword");
        public Button List = new Button("List");
        public Button NextPage = new Button("Next Page");
        public Button Quit = new Button("Quit");
        public Button Refine = new Button("Refine");
        public Button Remove = new Button("Remove from List", "REMOVE:{0}");
        public Button StartOver = new Button("Start Over");

        // Status
        public string Ascending = "Ascending";
        public string Count = "**Total results**: {0}";
        public string Descending = "Descending";
        public string Filter = "**Filter**: {0}";
        public string Keywords = "**Keywords**: {0}";
        public string Page = "**Page**: {0}";
        public string Selected = "**Kept so far**: {0}";
        public string Sort = "**Sort**: {0}";

        // Labels (UI) and Messages
        public string AnyLabel = "Any {0}";
        public string AnyMessage = "Any";
        public string AnyNumberLabel = "Any number of {0}";
        public string RemoveKeywordMessage = "remove {0}";
    }
}