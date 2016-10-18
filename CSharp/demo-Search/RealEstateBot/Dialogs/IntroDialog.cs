namespace RealEstateBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Search.Models;
    using Search.Services;
    using Search.Dialogs;
    using System.Web;
    using System.IO;
    using System.Configuration;
    using Search.LUIS;

    [Serializable]
    public class IntroDialog : IDialog<object>
    {
        private ISearchClient searchClient;

        public IntroDialog(ISearchClient searchClient)
        {
            SetField.NotNull(out this.searchClient, nameof(searchClient), searchClient);
            var fields = searchClient.Schema.Fields;

            // This is not needed is you supply the web.config SearchDialogsServiceAdminKey because it will come from the service itself
            if (fields.Count == 0)
            {
                fields.Add("listingId", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = true, IsRetrievable = true, IsSearchable = false, IsSortable = false, Name = "listingId", Type = typeof(String) });
                fields.Add("beds", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "beds", Type = typeof(Int32) });
                fields.Add("baths", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "baths", Type = typeof(Int32) });
                fields.Add("description", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Name = "description", Type = typeof(String) });
                fields.Add("sqft", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "sqft", Type = typeof(Int32) });
                fields.Add("daysOnMarket", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "daysOnMarket", Type = typeof(Int32) });
                fields.Add("status", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Name = "status", Type = typeof(String) });
                fields.Add("source", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Name = "source", Type = typeof(String) });
                fields.Add("number", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Name = "number", Type = typeof(String) });
                fields.Add("street", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Name = "street", Type = typeof(String) });
                fields.Add("unit", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Name = "unit", Type = typeof(String) });
                fields.Add("type", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Name = "type", Type = typeof(String) });
                fields.Add("city", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Name = "city", Type = typeof(String) });
                fields.Add("cityPhonetic", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Name = "cityPhonetic", Type = typeof(String) });
                fields.Add("district", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "district", Type = typeof(String) });
                fields.Add("region", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Name = "region", Type = typeof(String) });
                fields.Add("zipcode", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Name = "zipcode", Type = typeof(String) });
                fields.Add("countryCode", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Name = "countryCode", Type = typeof(String) });
                fields.Add("location", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "location", Type = typeof(Microsoft.Spatial.GeographyPoint) });
                fields.Add("price", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Name = "price", Type = typeof(Int64) });
                fields.Add("thumbnail", new SearchField { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Name = "thumbnail", Type = typeof(String) });
            }
        }

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(this.StartSearchDialog);
            return Task.CompletedTask;
        }

        public async Task StartSearchDialog(IDialogContext context, IAwaitable<IMessageActivity> input)
        {
            var key = ConfigurationManager.AppSettings["LUISSubscriptionKey"];
            // TODO: Remove this
            if (string.IsNullOrWhiteSpace(key)) key = "bca5f68330234c2f9634610b48eea2da";
            var appName = "testImport4";
            var id = await LUISTools.GetOrImportModelAsync(key, appName, Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\realestatemodel.json"));
            context.Call(new SearchLanguageDialog(this.searchClient.Schema, key, id), DoneSpec);
            // context.Call(new RealEstateSearchDialog(this.searchClient), this.Done);
        }

        public Task DoneSpec(IDialogContext context, IAwaitable<SearchSpec> spec)
        {
            return Task.CompletedTask;
        }

        public async Task Done(IDialogContext context, IAwaitable<IList<SearchHit>> input)
        {
            var selection = await input;

            if (selection != null && selection.Any())
            {
                string list = string.Join("\n\n", selection.Select(s => $"* {s.Title} ({s.Key})"));
                await context.PostAsync($"Done! For future reference, you selected these properties:\n\n{list}");
            }

            context.Done<object>(null);
        }
    }
}
