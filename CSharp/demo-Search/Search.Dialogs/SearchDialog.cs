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
        public string InitialPrompt = "What would you like to find?";
        public string RefinePrompt = "Refine your search";
        public string FacetPrompt = "What facet would you like to refine by?";
        public string FacetValuePrompt = "What value for {0} would you like to filter by?";
        public string NotUnderstoodPrompt = "I did not understand what you said";
        public string UnknownItemPrompt = "That is not an item in the current results.";
        public string AddedToListPrompt = "{0} was added to your list.";
        public string RemovedFromListPrompt = "{0} was removed from your list.";
        public string ListPrompt = "Here is what you have selected so far.";
        public string NotAddedPrompt = "You have not added anything yet.";
        public string MinimumPrompt = "What is the minimum value for {0}?";
        public string MaximumPrompt = "What is the maximum value for {0}?";

        // Buttons
        public Button Browse = new Button("Browse");
        public Button NextPage = new Button("Next Page");
        public Button List = new Button("List");
        public Button Add = new Button("Add to List", "ADD:{0}");
        public Button Remove = new Button("Remove from List", "REMOVE:{0}");
        public Button Quit = new Button("Quit");
        public Button Finished = new Button("Finished");
        public Button StartOver = new Button("Start Over");

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
        protected readonly ISearchHitStyler HitStyler;
        protected readonly bool MultipleSelection;
        protected readonly Button[] Refiners;
        protected Button[] LastButtons = null;
        protected string Refiner = null;
        private readonly IList<SearchHit> Selected = new List<SearchHit>();
        private IList<SearchHit> Found;

        [NonSerialized]
        protected Canonicalizer FieldCanonicalizer;

        [NonSerialized]
        protected Dictionary<string, Canonicalizer> ValueCanonicalizers;

        public SearchDialog(Prompts prompts, ISearchClient searchClient, string key, string model, SearchQueryBuilder queryBuilder = null,
            PromptStyler promptStyler = null,
            ISearchHitStyler searchHitStyler = null,
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
                        var label = field.Description();
                        defaultRefiners.Add(new Button(label));
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
            LastButtons = buttons;
            await context.PostAsync(msg);
        }

        public override Task StartAsync(IDialogContext context)
        {
            context.Wait(Intro);
            return Task.CompletedTask;
        }

        public async Task Intro(IDialogContext context, IAwaitable<IMessageActivity> message)
        {
            await PromptAsync(context, Prompts.InitialPrompt, Prompts.Browse, Prompts.Quit);
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
            if (text.StartsWith("ADD:"))
            {
                var id = text.Substring(4).Trim();
                await this.AddSelectedItem(context, id);
            }
            else if (text.StartsWith("REMOVE:"))
            {
                var id = text.Substring(7).Trim();
                await this.RemoveSelectedItem(context, id);
            }
            else
            {
                await base.MessageReceived(context, item);
            }
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await PromptAsync(context, Prompts.NotUnderstoodPrompt, LastButtons);
            context.Wait(MessageReceived);
        }

        [LuisIntent("Facet")]
        public async Task Facet(IDialogContext context, LuisResult result)
        {
            Canonicalizers();
            this.Refiner = FieldCanonicalizer.Canonicalize(result.Query);
            if (this.Refiner == null)
            {
                await this.Filter(context, result);
            }
            else
            {
                // TODO: Specify the facet and pick up the results
                var search = await this.ExecuteSearchAsync(this.Refiner);
                var field = SearchClient.Schema.Field(this.Refiner);
                var desc = field.Description();
                var buttons = new List<Button>();
                var choices = (from facet in search.Facets[this.Refiner] orderby facet.Value ascending select facet);
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
                    await PromptAsync(context, string.Format(Prompts.FacetValuePrompt, desc), buttons.ToArray());
                    context.Wait(MessageReceived);
                }
                else if (field.FilterPreference == PreferredFilter.RangeMin)
                {
                    PromptDialog.Number(context, MinValue, string.Format(this.Prompts.MinimumPrompt, desc));
                }
                else if (field.FilterPreference == PreferredFilter.RangeMax)
                {
                    PromptDialog.Number(context, MaxValue, string.Format(this.Prompts.MaximumPrompt, desc));
                }
                else if (field.FilterPreference == PreferredFilter.Range)
                {
                    PromptDialog.Number(context, RangeMin, string.Format(this.Prompts.MinimumPrompt, this.Refiner));
                }
            }
        }

        private async Task MinValue(IDialogContext context, IAwaitable<double> input)
        {
            var number = await input;
            this.QueryBuilder.Spec.Filter = FilterExpression.Combine(this.QueryBuilder.Spec.Filter, 
                new FilterExpression(Operator.GreaterThanOrEqual, this.SearchClient.Schema.Field(this.Refiner), number));
            await Search(context);
        }

        private async Task MaxValue(IDialogContext context, IAwaitable<double> input)
        {
            var number = await input;
            this.QueryBuilder.Spec.Filter = FilterExpression.Combine(this.QueryBuilder.Spec.Filter, 
                new FilterExpression(Operator.LessThanOrEqual, this.SearchClient.Schema.Field(this.Refiner), number));
            await Search(context);
        }

        private async Task RangeMin(IDialogContext context, IAwaitable<double> input)
        {
            var number = await input;
            this.QueryBuilder.Spec.Filter = FilterExpression.Combine(this.QueryBuilder.Spec.Filter,
                new FilterExpression(Operator.GreaterThanOrEqual, this.SearchClient.Schema.Field(this.Refiner), number));
            PromptDialog.Number(context, RangeMax, string.Format(this.Prompts.MaximumPrompt, this.Refiner));
        }

        private async Task RangeMax(IDialogContext context, IAwaitable<double> input)
        {
            var number = await input;
            this.QueryBuilder.Spec.Filter = FilterExpression.Combine(this.QueryBuilder.Spec.Filter,
                new FilterExpression(Operator.LessThanOrEqual, this.SearchClient.Schema.Field(this.Refiner), number));
            await Search(context);
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
            var removals = (from entity in entities where entity.Type == "Removal" select entity);
            var substrings = entities.UncoveredSubstrings(result.Query);
            foreach (var removal in removals)
            {
                foreach (var entity in entities)
                {
                    if (entity.Type == "Property"
                        && entity.StartIndex >= removal.StartIndex
                        && entity.EndIndex <= removal.EndIndex)
                    {
                        if (this.QueryBuilder.Spec.Filter != null)
                        {
                            this.QueryBuilder.Spec.Filter = this.QueryBuilder.Spec.Filter.Remove(this.SearchClient.Schema.Field(FieldCanonicalizer.Canonicalize(entity.Entity)));
                        }
                        else
                        {
                            break;
                        }
                    }
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
            await this.PromptAsync(context, Prompts.FacetPrompt, this.Refiners);
            context.Wait(MessageReceived);
        }

        [LuisIntent("List")]
        public async Task List(IDialogContext context, LuisResult result)
        {
            await ShowList(context);
        }

        private async Task ShowList(IDialogContext context)
        {
            if (this.Selected.Count == 0)
            {
                await this.PromptAsync(context, this.Prompts.NotAddedPrompt, LastButtons);
            }
            else
            {
                var message = context.MakeMessage();
                this.HitStyler.Show(ref message, (IReadOnlyList<SearchHit>)this.Selected, this.Prompts.ListPrompt, this.Prompts.Remove);
                await context.PostAsync(message);
                await PromptAsync(context, this.Prompts.RefinePrompt, this.Prompts.Finished, this.Prompts.Quit, this.Prompts.StartOver, this.Prompts.Browse);
            }
            context.Wait(MessageReceived);
        }

        [LuisIntent("StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            this.QueryBuilder.Reset();
            await Search(context);
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
                await PromptAsync(context, Prompts.RefinePrompt, Prompts.Browse, Prompts.List, Prompts.Finished, Prompts.Quit, Prompts.StartOver);
            }
            else
            {
                var message = context.MakeMessage();
                this.Found = response.Results.ToList();
                this.HitStyler.Show(
                    ref message,
                    (IReadOnlyList<SearchHit>)this.Found,
                    SearchDescription(),
                    this.Prompts.Add
                    );
                await context.PostAsync(message);
                await PromptAsync(context, Prompts.RefinePrompt, Prompts.Browse, Prompts.NextPage, Prompts.List, Prompts.Finished, Prompts.Quit, Prompts.StartOver);
            }
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
                await PromptAsync(context, this.Prompts.UnknownItemPrompt);
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
                    await PromptAsync(context, string.Format(this.Prompts.AddedToListPrompt, hit.Title));
                    context.Wait(MessageReceived);
                }
                else
                {
                    context.Done(this.Selected);
                }
            }
        }

        protected virtual async Task RemoveSelectedItem(IDialogContext context, string selection)
        {
            SearchHit hit = this.Found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, this.Prompts.UnknownItemPrompt);
                context.Wait(MessageReceived);
            }
            else
            {
                await PromptAsync(context, string.Format(this.Prompts.RemovedFromListPrompt, hit.Title));
                this.Selected.Remove(hit);
                await ShowList(context);
            }
        }
    }
}
