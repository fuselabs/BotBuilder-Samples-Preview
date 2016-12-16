using Search.Dialogs.UserInteraction;

namespace JobListingBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Bot.Builder.Dialogs;
    using Search.Dialogs;
    using Search.Models;
    using Search.Services;
    using System.Configuration;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.IO;
    using Search.Utilities;
    using Microsoft.Bot.Builder.Internals.Fibers;

    [Serializable]
    public class JobListingSearchDialog : IDialog
    {
        private readonly ISearchClient SearchClient;
        private const string LUISKey = "LUISSubscriptionKey";

        public JobListingSearchDialog(ISearchClient searchClient)
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
            var cts = new CancellationTokenSource();
            var id = await LUISTools.GetOrCreateModelAsync(key, Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\JobListingModel.json"), cts.Token);
            context.Call(new SearchDialog(new Prompts(), this.SearchClient, key, id, multipleSelection: true,
                refiners: new string[] { "business_title", "agency", "work_location", "tags" }), Done);
        }

        public async Task Done(IDialogContext context, IAwaitable<IList<SearchHit>> input)
        {
            var selection = await input;

            if (selection != null && selection.Any())
            {
                string list = string.Join("\n\n", selection.Select(s => $"* {s.Title} ({s.Key})"));
                await context.PostAsync($"Done! For future reference, you selected these jobs:\n\n{list}");
            }

            context.Done<object>(null);
        }
    }
}
