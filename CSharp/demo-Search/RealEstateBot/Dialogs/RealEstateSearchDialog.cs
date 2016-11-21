namespace RealEstateBot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Search.Dialogs;
    using Search.Models;
    using Search.Services;
    using Search.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    [Serializable]
    public class RealEstateSearchDialog : IDialog
    {
        private readonly ISearchClient SearchClient;
        private const string LUISKey = "LUISSubscriptionKey";

        public RealEstateSearchDialog(ISearchClient searchClient)
        {
            SetField.NotNull(out this.SearchClient, nameof(SearchClient), searchClient);
        }

        public async Task StartAsync(IDialogContext context)
        {
            var key = ConfigurationManager.AppSettings[LUISKey];
            if (string.IsNullOrWhiteSpace(key))
            {
                // For local debugging of the sample without checking in your key
                key = System.Environment.GetEnvironmentVariable(LUISKey);
            }
            context.PostAsync("Welcome to the real estate search bot!");
            var cts = new CancellationTokenSource();
            var id = await LUISTools.GetOrCreateModelAsync(key, Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\RealEstateModel.json"), cts.Token);
            context.Call(new SearchDialog(new Prompts(),
                this.SearchClient, key, id, multipleSelection: true,
                refiners: new string[] {"type", "beds", "baths", "sqft", "price",
                                        "city", "district", "region",
                                        "daysOnMarket", "status" }), Done);
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
