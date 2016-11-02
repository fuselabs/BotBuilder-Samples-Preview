namespace RealEstateBot.Dialogs
{
    using Search.Dialogs;
    using Search.Models;
    using Search.Services;
    using System;

    [Serializable]
    public class RealEstateSearchDialog : SearchDialog
    {
        private static readonly string[] TopRefiners = { "region", "city", "type", "beds", "baths", "price", "daysOnMarket", "sqft" };

        public RealEstateSearchDialog(ISearchClient searchClient, string key, string model) 
            : base(searchClient, key, model, multipleSelection: true)
        {
        }

        protected override string[] GetTopRefiners()
        {
            return TopRefiners;
        }
    }
}
