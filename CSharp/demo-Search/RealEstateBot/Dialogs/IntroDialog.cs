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
            var id = await LUISTools.GetOrCreateModelAsync(key, appName, Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\realestatemodel.json"));
            // context.Call(new SearchLanguageDialog(this.searchClient.Schema, key, id), DoneSpec);
            context.Call(new RealEstateSearchDialog(this.searchClient, key, id), this.Done);
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
