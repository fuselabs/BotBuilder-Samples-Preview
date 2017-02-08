using System;

namespace Search.Dialogs.UserInteraction
{
    [Serializable]
    public class Prompts
    {
        // Prompts
        public const string InitialPrompt = "Please describe in your own words what you would like to find?";
        public const string RefinePrompt = "Refine your search";
        public const string FacetPrompt = "What facet would you like to refine by?";
        public const string FacetValuePrompt = "What value for {0} would you like to filter by?";
        public const string NotUnderstoodPrompt = "I did not understand what you said";
        public const string UnknownItemPrompt = "That is not an item in the current results.";
        public const string AddedToListPrompt = "{0} was added to your list.";
        public const string RemovedFromListPrompt = "{0} was removed from your list.";
        public const string ListPrompt = "Here is what you have selected so far.";
        public const string NotAddedPrompt = "You have not added anything yet.";
        public const string NoValuesPrompt = "There are no values to filter by for {0}.";
        public const string FilterPrompt = "Enter a filter for {0} like \"no more than 4\".";
        public const string NoResultsPrompt = "Your search found no results so I undid your last constraint.  You can refine again.";

        // Buttons
        public readonly Button Browse = new Button("Browse");
        public readonly Button NextPage = new Button("Next Page");
        public readonly Button List = new Button("List");
        public readonly Button Add = new Button("Add to List", "ADD:{0}");
        public readonly Button Remove = new Button("Remove from List", "REMOVE:{0}");
        public readonly Button Quit = new Button("Quit");
        public readonly Button Finished = new Button("Finished");
        public readonly Button StartOver = new Button("Start Over");

        // Status
        public const string Filter = "**Filter**: {0}";
        public const string Keywords = "**Keywords**: {0}";
        public const string Sort = "**Sort**: {0}";
        public const string Page = "**Page**: {0}";
        public const string Count = "**Total results**: {0}";
        public const string Selected = "**Kept so far**: {0}";
        public const string Ascending = "Ascending";
        public const string Descending = "Descending";

        // Facet messages
        public const string AnyNumberLabel = "Any number of {0}";
        public const string AnyLabel = "Any {0}";
        public const string AnyMessage = "Any";
    }
}