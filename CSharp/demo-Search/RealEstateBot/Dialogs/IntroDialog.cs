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
    using Search.Utilities;
    using Newtonsoft.Json;

    [Serializable]
    public class IntroDialog : IDialog<object>
    {
        private ISearchClient searchClient;

        public IntroDialog(ISearchClient searchClient)
        {
            SetField.NotNull(out this.searchClient, nameof(searchClient), searchClient);
            var schema = searchClient.Schema;

            // This is not needed is you supply the web.config SearchDialogsServiceAdminKey because it will come from the service itself
            if (schema.Fields.Count == 0)
            {
                schema.DefaultCurrencyProperty = "price";
                schema.AddField(new SearchField("listingId") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = true, IsRetrievable = true, IsSearchable = false, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("beds") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Int32) });
                schema.AddField(new SearchField("baths") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Int32) });
                schema.AddField(new SearchField("description") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("sqft") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Int32) });
                schema.AddField(new SearchField("daysOnMarket") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Int32) });
                schema.AddField(new SearchField("status") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("source") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("number") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("street") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("unit") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("type") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Type = typeof(String) });
                schema.AddField(new SearchField("city") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Type = typeof(String) });
                // schema.AddField(new SearchField("") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Type = typeof(String) });
                schema.AddField(new SearchField("district") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(String) });
                schema.AddField(new SearchField("region") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Type = typeof(String) });
                schema.AddField(new SearchField("zipcode") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Type = typeof(String) });
                schema.AddField(new SearchField("countryCode") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = true, IsSortable = true, Type = typeof(String) });
                schema.AddField(new SearchField("location") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Microsoft.Spatial.GeographyPoint) });
                schema.AddField(new SearchField("price") { FilterPreference = PreferredFilter.None, IsFacetable = true, IsFilterable = true, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = true, Type = typeof(Int64) });
                // schema.AddField(new SearchField("") { FilterPreference = PreferredFilter.None, IsFacetable = false, IsFilterable = false, IsKey = false, IsRetrievable = true, IsSearchable = false, IsSortable = false, Type = typeof(String) });
            }
            // TODO: Remove this
            schema.Save(@"c:\tmp\schema.json");
            var s = SearchSchema.Load(@"C:\tmp\schema.json");
        }

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(this.StartSearchDialog);
            return Task.CompletedTask;
        }

        public async Task StartSearchDialog(IDialogContext context, IAwaitable<IMessageActivity> input)
        {
            var key = ConfigurationManager.AppSettings["LUISSubscriptionKey"];
            var appName = "realestatemodel";
            // TODO: Remove this
            if (string.IsNullOrWhiteSpace(key)) key = "bca5f68330234c2f9634610b48eea2da";
            var old = await LUISTools.GetModelAsync(key, appName);
            if (false && old != null)
            {
                await LUISTools.DeleteModelAsync(key, (string)old["ID"]);
            }

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
