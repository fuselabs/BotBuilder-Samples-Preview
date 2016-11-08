namespace Search.Dialogs
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
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Builder.Luis;
    using System.Text;

    public delegate SearchField CanonicalizerDelegate(string propertyName);

    [Serializable]
    public class Button
    {
        public Button(string label, string message = null)
        {
            Label = label;
            Message = message ?? label;
        }

        public string Label;
        public string Message;
    }

    [Serializable]
    public class Prompts
    {
        // Prompts
        public string Initial = "What would you like to find?";
        public string Result = "Refine your search";
        public string ChooseRefine = "What facet would you like to refine by?";
        public string ChooseValue = "What value for {0} would you like to filter by?";
        public string NotUnderstood = "I did not understand what you said";
        public string UnknownItem = "That is not an item in the current results.";
        public string AddedToList = " was added to your list.";

        // Buttons
        public Button Browse = new Button("Browse");
        public Button Quit = new Button("Quit");
        public Button Finished = new Button("Finished");
        public Button List = new Button("List");
        public Button NextPage = new Button("Next Page");

        // Status
        public string Filter = "Filter: ";
        public string Keywords = "Keywords: ";
        public string Sort = "Sort: ";
        public string Page = "Page: ";
        public string Count = "Total results: ";
        public string Ascending = "Ascending";
        public string Descending = "Descending";

        // Facets
        public string Any = "Any number of ";
    }

    [Serializable]
    public class SearchDialog : LuisDialog<IList<SearchHit>>
    {
        protected readonly Prompts Prompts;
        protected readonly ISearchClient SearchClient;
        protected readonly SearchQueryBuilder QueryBuilder;
        protected readonly PromptStyler PromptStyler;
        protected readonly PromptStyler HitStyler;
        protected readonly bool MultipleSelection;
        protected readonly Button[] Refiners;
        private readonly IList<SearchHit> Selected = new List<SearchHit>();
        private IList<SearchHit> Found;

        [NonSerialized]
        protected Canonicalizer FieldCanonicalizer;

        [NonSerialized]
        protected Dictionary<string, Canonicalizer> ValueCanonicalizers;

        public SearchDialog(Prompts prompts, ISearchClient searchClient, string key, string model, SearchQueryBuilder queryBuilder = null,
            PromptStyler promptStyler = null,
            PromptStyler searchHitStyler = null,
            bool multipleSelection = false,
            IEnumerable<Button> refiners = null)
            : base(new LuisService(new LuisModelAttribute(model, key)))
        {
            SetField.NotNull(out this.Prompts, nameof(Prompts), prompts);
            SetField.NotNull(out this.SearchClient, nameof(searchClient), searchClient);
            this.QueryBuilder = queryBuilder ?? new SearchQueryBuilder();
            this.HitStyler = searchHitStyler ?? new SearchHitStyler();
            this.PromptStyler = promptStyler ?? new PromptStyler();
            this.MultipleSelection = multipleSelection;
            if (refiners == null)
            {
                var defaultRefiners = new List<Button>();
                foreach (var field in this.SearchClient.Schema.Fields.Values)
                {
                    if (field.IsFacetable && field.NameSynonyms.Alternatives.Any())
                    {
                        var button = new Button(field.NameSynonyms.Canonical, field.NameSynonyms.Alternatives.First());
                        defaultRefiners.Add(button);
                    }
                }
                refiners = defaultRefiners;
            }
            Refiners = refiners.ToArray();
        }

        protected async Task PromptAsync(IDialogContext context, string prompt, params Button[] buttons)
        {
            var msg = context.MakeMessage();
            this.PromptStyler.Apply(ref msg, prompt, (from button in buttons select button.Label).ToList(), (from button in buttons select button.Message).ToList());
            await context.PostAsync(msg);
        }

        public override Task StartAsync(IDialogContext context)
        {
            context.Wait(Intro);
            return Task.CompletedTask;
        }

        public async Task Intro(IDialogContext context, IAwaitable<IMessageActivity> message)
        {
            await PromptAsync(context, Prompts.Initial, Prompts.Browse, Prompts.Quit);
            context.Wait(MessageReceived);
        }

        protected override IntentRecommendation BestIntentFrom(LuisResult result)
        {
            var best = (from intent in result.Intents
                        let score = intent.Score ?? 0.0
                        where score > 0.3
                        orderby score descending
                        select intent).FirstOrDefault();
            if (best == null)
            {
                best = new IntentRecommendation("Filter", 0.0);
            }
            return best;
        }

        protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            var message = await item;
            var text = message.Text.Trim();
            if (text.StartsWith("ID:"))
            {
                var id = text.Substring(3).Trim();
                await this.AddSelectedItem(context, id);
            }
            else
            {
                await base.MessageReceived(context, item);
            }
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await PromptAsync(context, Prompts.NotUnderstood);
            context.Wait(MessageReceived);
        }

        [LuisIntent("Facet")]
        public async Task Facet(IDialogContext context, LuisResult result)
        {
            Canonicalizers();
            var fieldName = FieldCanonicalizer.Canonicalize(result.Query);
            if (fieldName == null)
            {
                await this.Filter(context, result);
            }
            else
            {
                // TODO: Specify the facet and pick up the results
                var search = await this.ExecuteSearchAsync(fieldName);
                var field = SearchClient.Schema.Field(fieldName);
                var desc = field.NameSynonyms.Alternatives.First() ?? fieldName;
                var buttons = new List<Button>();
                var choices = (from facet in search.Facets[fieldName] orderby facet.Value ascending select facet);
                if (field.FilterPreference == PreferredFilter.None)
                {
                    foreach (var choice in choices)
                    {
                        buttons.Add(new Button($"{choice.Value} {desc}", $"{choice.Value} ({choice.Count})"));
                    }
                }
                else if (field.FilterPreference == PreferredFilter.MinValue)
                {
                    var total = choices.Sum((choice) => choice.Count);
                    foreach (var choice in choices)
                    {
                        buttons.Add(new Button($"{choice.Value}+ {desc}", $"{choice.Value}+ ({total})"));
                        total -= choice.Count;
                    }
                }
                else if (field.FilterPreference == PreferredFilter.MaxValue)
                {
                    long total = 0;
                    foreach (var choice in choices)
                    {
                        total += choice.Count;
                        buttons.Add(new Button($"<= {choice.Value} {desc}", $"<= {choice.Value} ({total})"));
                    }
                }
                if (buttons.Any())
                {
                    buttons.Add(new Button($"Any number of {desc}", "Any"));
                    await PromptAsync(context, string.Format(Prompts.ChooseValue, desc), buttons.ToArray());
                }
                /* TODO: How to handle these?  
                else if (field.FilterPreference == PreferredFilter.RangeMin)
                {
                    PromptDialog.Number(context, MinRefiner, $"What is the minimum {this.Refiner}?");
                }
                else if (field.FilterPreference == PreferredFilter.RangeMax)
                {
                    PromptDialog.Number(context, MinRefiner, $"What is the maximum {this.Refiner}?");
                }
                else if (field.FilterPreference == PreferredFilter.Range)
                {
                    PromptDialog.Number(context, GetRangeMin, $"What is the minimum {this.Refiner}?");
                }
                */
                context.Wait(MessageReceived);
            }
        }

        private void Canonicalizers()
        {
            if (FieldCanonicalizer == null)
            {
                FieldCanonicalizer = new Canonicalizer();
                ValueCanonicalizers = new Dictionary<string, Canonicalizer>();
                foreach (var field in this.SearchClient.Schema.Fields.Values)
                {
                    FieldCanonicalizer.Add(field.NameSynonyms);
                    ValueCanonicalizers[field.Name] = new Canonicalizer(field.ValueSynonyms);
                }
            }
        }

        [LuisIntent("Filter")]
        public async Task Filter(IDialogContext context, LuisResult result)
        {
            Canonicalizers();
            bool isCurrency;
            var entities = result.Entities == null ? new List<EntityRecommendation>() : (from entity in result.Entities where entity.Type != "Number" || !double.IsNaN(ParseNumber(entity.Entity, out isCurrency)) select entity).ToList();
            var comparisons = (from entity in entities
                               where entity.Type == "Comparison"
                               select new ComparisonEntity(entity)).ToList();
            var attributes = (from entity in entities
                              where this.SearchClient.Schema.Fields.ContainsKey(entity.Type)
                              select new FilterExpression(Operator.Equal, this.SearchClient.Schema.Field(entity.Type),
                                this.ValueCanonicalizers[entity.Type].Canonicalize(entity.Entity)));
            var removals = (from entity in entities where entity.Type == "Attribute" select entity);
            var substrings = entities.UncoveredSubstrings(result.Query);
            foreach(var removal in removals)
            {
                if (this.QueryBuilder.Spec.Filter != null)
                {
                    this.QueryBuilder.Spec.Filter = this.QueryBuilder.Spec.Filter.Remove(this.SearchClient.Schema.Field(FieldCanonicalizer.Canonicalize(removal.Entity)));
                }
                else
                {
                    break;
                }
            }
            foreach (var entity in entities)
            {
                foreach (var comparison in comparisons)
                {
                    comparison.AddEntity(entity);
                }
            }
            var ranges = from comparison in comparisons
                         let range = Resolve(comparison)
                         where range != null
                         select range;
            var filter = ranges.GenerateFilterExpression(Operator.And);
            filter = attributes.GenerateFilterExpression(Operator.And, filter);
            if (this.QueryBuilder.Spec.Filter != null)
            {
                this.QueryBuilder.Spec.Filter = FilterExpression.Combine(this.QueryBuilder.Spec.Filter.Remove(filter), filter, Operator.And);
            }
            else
            {
                this.QueryBuilder.Spec.Filter = filter;
            }
            if (this.QueryBuilder.Spec.Text != null)
            {
                substrings = new string[] { this.QueryBuilder.Spec.Text }.Union(substrings);
            }
            this.QueryBuilder.Spec.Text = string.Join(" ", substrings);
            await Search(context);
        }

        [LuisIntent("NextPage")]
        public async Task NextPage(IDialogContext context, LuisResult result)
        {
            this.QueryBuilder.PageNumber++;
            await Search(context);
        }

        [LuisIntent("Refine")]
        public async Task Refine(IDialogContext context, LuisResult result)
        {
            await this.PromptAsync(context, Prompts.ChooseRefine, this.Refiners);
            context.Wait(MessageReceived);
        }

        [LuisIntent("List")]
        public Task List(IDialogContext context, LuisResult result)
        {
            // TODO: Implement
            return Task.CompletedTask;
        }

        [LuisIntent("StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            this.QueryBuilder.Reset();
            await StartAsync(context);
        }

        [LuisIntent("Quit")]
        public Task Quit(IDialogContext context, LuisResult result)
        {
            context.Done<IList<SearchHit>>(null);
            return Task.CompletedTask;
        }

        [LuisIntent("Done")]
        public Task Done(IDialogContext context, LuisResult result)
        {
            context.Done(Found);
            return Task.CompletedTask;
        }

        private double ParseNumber(string entity, out bool isCurrency)
        {
            isCurrency = false;
            double multiply = 1.0;
            if (entity.StartsWith("$"))
            {
                isCurrency = true;
                entity = entity.Substring(1);
            }
            if (entity.EndsWith("k"))
            {
                multiply = 1000.0;
                entity = entity.Substring(0, entity.Length - 1);
            }
            double result;
            if (double.TryParse(entity, out result))
            {
                result *= multiply;
            }
            else
            {
                result = double.NaN;
            }
            return result;
        }

        private Range Resolve(ComparisonEntity c)
        {
            Range range = null;
            var propertyName = this.FieldCanonicalizer.Canonicalize(c.Property?.Entity);
            if (propertyName != null)
            {
                range = new Range { Property = this.SearchClient.Schema.Field(propertyName) };
                bool isCurrency;
                var lower = c.Lower == null ? double.NegativeInfinity : ParseNumber(c.Lower.Entity, out isCurrency);
                var upper = c.Upper == null ? double.PositiveInfinity : ParseNumber(c.Upper.Entity, out isCurrency);
                if (c.Operator == null)
                {
                    // This is the case where we just have naked values
                    range.IncludeLower = true;
                    range.IncludeUpper = true;
                    upper = lower;
                }
                else
                {
                    switch (c.Operator.Entity)
                    {
                        case ">=":
                        case "+":
                        case "greater than or equal":
                        case "at least":
                        case "no less than":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            upper = double.PositiveInfinity;
                            break;

                        case ">":
                        case "greater than":
                        case "more than":
                            range.IncludeLower = false;
                            range.IncludeUpper = true;
                            upper = double.PositiveInfinity;
                            break;

                        case "-":
                        case "between":
                        case "and":
                        case "or":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            break;

                        case "<=":
                        case "no more than":
                        case "less than or equal":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            upper = lower;
                            lower = double.NegativeInfinity;
                            break;

                        case "<":
                        case "less than":
                            range.IncludeLower = true;
                            range.IncludeUpper = false;
                            upper = lower;
                            lower = double.NegativeInfinity;
                            break;

                        case "any":
                        case "any number of":
                            upper = double.PositiveInfinity;
                            lower = double.NegativeInfinity;
                            break;

                        default: throw new ArgumentException($"Unknown operator {c.Operator.Entity}");
                    }
                }
                range.Lower = lower;
                range.Upper = upper;
            }
            return range;
        }

        private string SearchDescription()
        {
            var builder = new StringBuilder();
            var filter = this.QueryBuilder.Spec.Filter;
            var text = this.QueryBuilder.Spec.Text;
            var sorts = this.QueryBuilder.Spec.Sort;
            if (filter != null)
            {
                builder.Append(Prompts.Filter);
                builder.AppendLine(filter.ToString());
                builder.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine($"{Prompts.Keywords}{text}");
                builder.AppendLine();
            }
            if (sorts.Count() > 0)
            {
                builder.Append(Prompts.Sort);
                var prefix = "";
                foreach (var sort in sorts)
                {
                    var dir = sort.Direction == SortDirection.Ascending ? Prompts.Ascending : Prompts.Descending;
                    builder.Append($"{prefix}{sort.Field} {dir}");
                    prefix = ", ";
                }
                builder.AppendLine();
                builder.AppendLine();
            }
            builder.AppendLine($"{Prompts.Page}{this.QueryBuilder.PageNumber}");
            return builder.ToString();
        }

        public async Task Search(IDialogContext context)
        {
            var response = await this.ExecuteSearchAsync();
            if (response.Results.Count() == 0)
            {
                // TODO: No results ,what would you like to change?
                await PromptAsync(context, Prompts.Result, Prompts.Browse, Prompts.List, Prompts.Finished, Prompts.Quit);
            }
            else
            {
                var message = context.MakeMessage();
                this.Found = response.Results.ToList();
                this.HitStyler.Apply(
                    ref message,
                    SearchDescription(),
                    (IReadOnlyList<SearchHit>)this.Found);
                await context.PostAsync(message);
                await PromptAsync(context, Prompts.Result, Prompts.Browse, Prompts.NextPage, Prompts.List, Prompts.Finished, Prompts.Quit);
            }
            /*
            var text = spec.Text;

            if (this.MultipleSelection && text != null && text.ToLowerInvariant() == "list")
            {
                await this.ListAddedSoFar(context);
                await this.InitialPrompt(context);
            }
            else
            {
                this.QueryBuilder.Spec = spec;
                var response = await this.ExecuteSearchAsync();
                if (response.Results.Count() == 0)
                {
                    await this.NoResultsConfirmRetry(context);
                }
                else
                {
                    var message = context.MakeMessage();
                    this.found = response.Results.ToList();
                    this.HitStyler.Apply(
                        ref message,
                        "Here are a few good options I found:",
                        (IReadOnlyList<SearchHit>)this.found);
                    await context.PostAsync(message);
                    await context.PostAsync(
                        this.MultipleSelection ?
                        "You can select one or more to add to your list, *list* what you've selected so far, *refine* these results, see *more* or search *again*." :
                        "You can select one, *refine* these results, see *more* or search *again*.");
                    context.Wait(this.ActOnSearchResults);
                }
            }
            */
            context.Wait(MessageReceived);
        }

        protected async Task<GenericSearchResult> ExecuteSearchAsync(string facet = null)
        {
            return await this.SearchClient.SearchAsync(this.QueryBuilder, facet);
        }

        protected virtual async Task AddSelectedItem(IDialogContext context, string selection)
        {
            SearchHit hit = this.Found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, this.Prompts.UnknownItem);
                context.Wait(MessageReceived);
            }
            else
            {
                if (!this.Selected.Any(h => h.Key == hit.Key))
                {
                    this.Selected.Add(hit);
                }

                if (this.MultipleSelection)
                {
                    await PromptAsync(context, $"'{hit.Title}'{this.Prompts.AddedToList}");
                    context.Wait(MessageReceived);
                }
                else
                {
                    context.Done(this.Selected);
                }
            }
        }

        /*
        protected async Task InitialSearch(IDialogContext context, IAwaitable<SearchSpec> spec)
        {
            await this.Search(context, await spec);
        }

        protected virtual Task NoResultsConfirmRetry(IDialogContext context)
        {
            PromptDialog.Confirm(context, this.ShouldRetry, "Sorry, I didn't find any matches. Do you want to retry your search?");
            return Task.CompletedTask;
        }

        protected virtual async Task ListAddedSoFar(IDialogContext context)
        {
            var message = context.MakeMessage();
            if (this.selected.Count == 0)
            {
                await context.PostAsync("You have not added anything yet.");
            }
            else
            {
                this.HitStyler.Apply(ref message, "Here's what you've added to your list so far.", (IReadOnlyList<SearchHit>)this.selected);
                await context.PostAsync(message);
            }
        }

        protected virtual async Task UnkownActionOnResults(IDialogContext context, string action)
        {
            await context.PostAsync("Not sure what you mean. You can search *again*, *refine*, *list* or select one of the items above. Or are you *done*?");
            context.Wait(this.ActOnSearchResults);
        }

        protected virtual async Task ShouldContinueSearching(IDialogContext context, IAwaitable<bool> input)
        {
            try
            {
                bool shouldContinue = await input;
                if (shouldContinue)
                {
                    await this.InitialPrompt(context);
                }
                else
                {
                    context.Done(this.selected);
                }
            }
            catch (TooManyAttemptsException)
            {
                context.Done(this.selected);
            }
        }

        protected void SelectRefiner(IDialogContext context)
        {
            var dialog = new SearchSelectRefinerDialog(this.GetTopRefiners(), this.QueryBuilder);
            context.Call(dialog, this.Refine);
        }

        protected async Task Refine(IDialogContext context, IAwaitable<string> input)
        {
            string refiner = await input;

            if (!string.IsNullOrWhiteSpace(refiner))
            {
                var dialog = new SearchRefineDialog(this.SearchClient, refiner, this.QueryBuilder.Spec.Filter);
                context.Call(dialog, this.ResumeFromRefine);
            }
            else
            {
                await this.Search(context, null);
            }
        }

        protected async Task ResumeFromRefine(IDialogContext context, IAwaitable<FilterExpression> filter)
        {
            this.QueryBuilder.Spec.Filter = await filter;
            await this.Search(context, this.QueryBuilder.Spec);
        }

        protected abstract string[] GetTopRefiners();

        private async Task ShouldRetry(IDialogContext context, IAwaitable<bool> input)
        {
            try
            {
                bool retry = await input;
                if (retry)
                {
                    await this.InitialPrompt(context);
                }
                else
                {
                    context.Done<IList<SearchHit>>(null);
                }
            }
            catch (TooManyAttemptsException)
            {
                context.Done<IList<SearchHit>>(null);
            }
        }

        private async Task ActOnSearchResults(IDialogContext context, IAwaitable<IMessageActivity> input)
        {
            var activity = await input;
            var choice = activity.Text;

            switch (choice.ToLowerInvariant())
            {
                case "again":
                case "reset":
                    this.QueryBuilder.Reset();
                    await this.InitialPrompt(context);
                    break;

                case "more":
                    this.QueryBuilder.PageNumber++;
                    await this.Search(context, this.QueryBuilder.Spec);
                    break;

                case "refine":
                    this.SelectRefiner(context);
                    break;

                case "list":
                    await this.ListAddedSoFar(context);
                    context.Wait(this.ActOnSearchResults);
                    break;

                case "done":
                    context.Done(this.selected);
                    break;

                default:
                    await this.AddSelectedItem(context, choice);
                    break;
            }
        }
    */
    }
}
