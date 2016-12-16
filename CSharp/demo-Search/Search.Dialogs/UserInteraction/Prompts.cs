using System;

namespace Search.Dialogs.UserInteraction
{
    [Serializable]
    public class Prompts
    {
        // Prompts
        public string InitialPrompt = "Please describe in your own words what you would like to find?";
        public string RefinePrompt = "Refine your search";
        public string FacetPrompt = "What facet would you like to refine by?";
        public string FacetValuePrompt = "What value for {0} would you like to filter by?";
        public string NotUnderstoodPrompt = "I did not understand what you said";
        public string UnknownItemPrompt = "That is not an item in the current results.";
        public string AddedToListPrompt = "{0} was added to your list.";
        public string RemovedFromListPrompt = "{0} was removed from your list.";
        public string ListPrompt = "Here is what you have selected so far.";
        public string NotAddedPrompt = "You have not added anything yet.";
        public string NoValuesPrompt = "There are no values to filter by for {0}.";
        public string FilterPrompt = "Enter a filter for {0} like \"no more than 4\".";
        public string NoResultsPrompt = "Your search found no results so I undid your last constraint.  You can refine again.";

        // Buttons
        public Button Browse = new Button("Browse");
        public Button NextPage = new Button("Next Page");
        public Button List = new Button("List");
        public Button Add = new Button("Add to List", "ADD:{0}");
        public Button Remove = new Button("Remove from List", "REMOVE:{0}");
        public Button Quit = new Button("Quit");
        public Button Finished = new Button("Finished");
        public Button StartOver = new Button("Start Over");

        // Status
        public string Filter = "Filter: {0}";
        public string Keywords = "Keywords: {0}";
        public string Sort = "Sort: {0}";
        public string Page = "Page: {0}";
        public string Count = "Total results: {0}";
        public string Selected = "Kept {0} results so far.";
        public string Ascending = "Ascending";
        public string Descending = "Descending";

        // Facet messages
        public string AnyNumberLabel = "Any number of {0}";
        public string AnyLabel = "Any {0}";
        public string AnyMessage = "Any";
    }
}