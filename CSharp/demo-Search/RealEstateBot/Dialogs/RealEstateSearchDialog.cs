using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Internals.Fibers;
using Search.Dialogs;
using Search.Dialogs.UserInteraction;
using Search.Models;
using Search.Services;
using Search.Utilities;
using Microsoft.Bot.Connector;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.LUIS.API;

namespace RealEstateBot.Dialogs
{
    [Serializable]
    public class RealEstateSearchDialog : IDialog
    {
        private const string NameKey = "Name";
        private const string QueryKey = "LastQuery";
        private const string LUISKeyKey = "LUISSubscriptionKey";
        private readonly ISearchClient SearchClient;
        private SearchSpec Query = new SearchSpec();
        private SearchSpec LastQuery = null;
        private string LUISKey;
        private string ModelId;

        public RealEstateSearchDialog(ISearchClient searchClient)
        {
            SetField.NotNull(out SearchClient, nameof(SearchClient), searchClient);
        }

        public async Task StartAsync(IDialogContext context)
        {
            this.LUISKey = ConfigurationManager.AppSettings[LUISKeyKey];
            if (string.IsNullOrWhiteSpace(this.LUISKey))
            {
                // For local debugging of the sample without checking in your key
                this.LUISKey = Environment.GetEnvironmentVariable(LUISKeyKey);
            }
            var subscription = new Subscription("westus.api.cognitive.microsoft.com", this.LUISKey);
            var application = await subscription.GetOrImportApplicationAsync(
                        Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\RealEstateModel.json"),
                        context.CancellationToken);
            this.ModelId = application.ApplicationID;
            context.Wait(IgnoreFirstMessage);
        }

        private async Task IgnoreFirstMessage(IDialogContext context, IAwaitable<IMessageActivity> msg)
        {
            string name;
            if (context.UserData.TryGetValue(NameKey, out name))
            {
                await context.PostAsync($"Welcome back to the real estate bot {name}!");
                try
                {
                    byte[] lastQuery;
                    if (context.UserData.TryGetValue(QueryKey, out lastQuery))
                    {
                        using (var stream = new MemoryStream(lastQuery))
                        {
                            var formatter = new BinaryFormatter();
                            this.LastQuery = (SearchSpec)formatter.Deserialize(stream);
                        }
                        await context.PostAsync($@"**Last Search**

{this.LastQuery.Description()}");
                        context.Call(new PromptDialog.PromptConfirm("Do you want to start from your last search?", null, 1), UseLastSearch);
                    }
                    else
                    {
                        Search(context);
                    }
                }
                catch (Exception)
                {
                    context.UserData.RemoveValue(QueryKey);
                    Search(context);
                }
            }
            else
            {
                await context.PostAsync("Welcome to the real estate search bot!");
                context.Call(
                    new PromptDialog.PromptString("What is your name?", "What is your name?", 2),
                    GotName);
            }
        }

        public async Task GotName(IDialogContext context, IAwaitable<string> name)
        {
            var newName = await name;
            await context.PostAsync($"Good to meet you {newName}!");
            context.UserData.SetValue(NameKey, newName);
            Search(context);
        }

        private async Task UseLastSearch(IDialogContext context, IAwaitable<bool> answer)
        {
            if (await answer)
            {
                this.Query = this.LastQuery;
            }
            else
            {
                this.Query = new SearchSpec();
            }
            this.LastQuery = null;
            Search(context);
        }

        private void Search(IDialogContext context)
        {
            context.Call(new SearchDialog(new Prompts(),
                this.SearchClient, this.LUISKey, this.ModelId,
                multipleSelection: true,
                query: this.Query,
                refiners: new string[]
                {
                    "type", "beds", "baths", "sqft", "price",
                    "city", "Keyword", "district", "region",
                    "daysOnMarket", "status"
                }), Done);
        }

        private async Task Done(IDialogContext context, IAwaitable<IList<SearchHit>> input)
        {
            var selection = await input;

            if (selection != null && selection.Any())
            {
                var list = string.Join("\n\n", selection.Select(s => $"* {s.Title} ({s.Key})"));
                await context.PostAsync($"Done! For future reference, you selected these properties:\n\n{list}");
            }
            else
            {
                await context.PostAsync($"Sorry you could not find anything you liked--maybe next time!");
            }
            if (this.Query.HasNoConstraints)
            {
                // Reset name and query if no query
                context.UserData.RemoveValue(NameKey);
                context.UserData.RemoveValue(QueryKey);
            }
            else
            {
                this.Query.PageNumber = 0;
                using (var stream = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(stream, this.Query);
                    context.UserData.SetValue(QueryKey, stream.ToArray());
                }
            }
            context.Done<object>(null);
        }
    }
}