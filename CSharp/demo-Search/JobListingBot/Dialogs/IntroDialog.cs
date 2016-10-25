namespace JobListingBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Search.Dialogs;
    using Search.Models;
    using Search.Services;

    [Serializable]
    public class IntroDialog : IDialog<object>
    {
        protected readonly SearchQueryBuilder QueryBuilder = new SearchQueryBuilder();
        private readonly ISearchClient searchClient;

        public IntroDialog(ISearchClient searchClient)
        {
            SetField.NotNull(out this.searchClient, nameof(searchClient), searchClient);
            var schema = searchClient.Schema;
            // This is not needed is you supply the web.config SearchDialogsServiceAdminKey because it will come from the service itself
            if (schema.Fields.Count() == 0)
            {
                schema.AddField(new SearchField("business_title")
                {
                    FilterPreference = PreferredFilter.None,
                    IsFacetable = true,
                    IsFilterable = true,
                    IsKey = false,
                    IsRetrievable = true,
                    IsSearchable = true,
                    IsSortable = true,
                    Type = typeof(string)
                });
                schema.AddField(new SearchField("agency")
                {
                    FilterPreference = PreferredFilter.None,
                    IsFacetable = true,
                    IsFilterable = true,
                    IsKey = false,
                    IsRetrievable = true,
                    IsSearchable = true,
                    IsSortable = true,
                    Type = typeof(string)
                });
                schema.AddField(new SearchField("work_location")
                {
                    FilterPreference = PreferredFilter.None,
                    IsFacetable = true,
                    IsFilterable = true,
                    IsKey = false,
                    IsRetrievable = true,
                    IsSearchable = true,
                    IsSortable = true,
                    Type = typeof(string)
                });
                schema.AddField(new SearchField("tags")
                {
                    FilterPreference = PreferredFilter.None,
                    IsFacetable = true,
                    IsFilterable = true,
                    IsKey = false,
                    IsRetrievable = true,
                    IsSearchable = true,
                    IsSortable = false,
                    Name = "tags",
                    Type = typeof(string[])
                });
            }
        }

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(this.SelectTitle);
            return Task.CompletedTask;
        }

        public Task SelectTitle(IDialogContext context, IAwaitable<IMessageActivity> input)
        {
            context.Call(
                new SearchRefineDialog(
                    this.searchClient,
                    "business_title",
                    this.QueryBuilder,
                    prompt: "Hi! To get started, what kind of position are you looking for?"),
                this.StartSearchDialog);
            return Task.CompletedTask;
        }

        public async Task StartSearchDialog(IDialogContext context, IAwaitable<FilterExpression> input)
        {
            var title = await input;
            context.Call(new JobsDialog(this.searchClient, this.QueryBuilder), this.Done);
        }

        public async Task Done(IDialogContext context, IAwaitable<IList<SearchHit>> input)
        {
            var selection = await input;

            if (selection != null && selection.Any())
            {
                string list = string.Join("\n\n", selection.Select(s => $"* {s.Title} ({s.Key})"));
                await context.PostAsync($"Done! For future reference, you selected these job listings:\n\n{list}");
            }

            this.QueryBuilder.Reset();
            context.Done<object>(null);
        }
    }
}
