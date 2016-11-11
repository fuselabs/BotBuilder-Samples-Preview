namespace Search.Dialogs
{
    // TODO: List of things still to do
    // Switch to built-in currency and numbers 
    // Add spelling support
    // Remove noise words from begin/end of keywords
    // Provide a way to reset the keywords
    // Add support around locations
    // Values from facets are brittle.  We can fix this by having extract pull out canonical values.
    // Cannot handle street in RealEstate because of the way facet values are handled.
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
    using Azure;

    public delegate SearchField CanonicalizerDelegate(string propertyName);

    public class Range
    {
        public SearchField Property;
        public object Lower;
        public object Upper;
        public bool IncludeLower;
        public bool IncludeUpper;
    }

    class ComparisonEntity
    {
        public EntityRecommendation Entity;
        public EntityRecommendation Operator;
        public EntityRecommendation Lower;
        public EntityRecommendation Upper;
        public EntityRecommendation Property;

        public ComparisonEntity(EntityRecommendation comparison)
        {
            Entity = comparison;
        }

        public void AddEntity(EntityRecommendation entity)
        {
            if (entity.Type != "Comparison" && entity.StartIndex >= Entity.StartIndex && entity.EndIndex <= Entity.EndIndex)
            {
                switch (entity.Type)
                {
                    case "Currency": AddNumber(entity); break;
                    case "Value": AddNumber(entity); break;
                    case "Dimension": AddNumber(entity); break;
                    case "Operator": Operator = entity; break;
                    case "Property": Property = entity; break;
                }
            }
        }

        private void AddNumber(EntityRecommendation entity)
        {
            if (Lower == null)
            {
                Lower = entity;
            }
            else if (entity.StartIndex < Lower.StartIndex)
            {
                Upper = Lower;
                Lower = entity;
            }
            else
            {
                Upper = entity;
            }
        }
    }

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
        public string NoValuesPrompt = "There are no values to filter by for {0}.";
        public string FilterPrompt = "Enter a filter for {0} like \"no more than 4\".";
        public string NoResultsPrompt = "Your search found no results so I undid your last constraint.  You can refine again.";

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

        // Facet messages
        public string AnyNumberLabel = "Any number of {0}";
        public string AnyLabel = "Any {0}";
        public string AnyMessage = "Any";
    }

    [Serializable]
    public class SearchDialog : LuisDialog<IList<SearchHit>>
    {
        protected readonly Prompts Prompts;
        protected readonly ISearchClient SearchClient;
        protected readonly PromptStyler PromptStyler;
        protected readonly ISearchHitStyler HitStyler;
        protected readonly bool MultipleSelection;
        protected readonly Button[] Refiners;
        protected SearchQueryBuilder QueryBuilder;
        protected SearchQueryBuilder LastQueryBuilder;
        protected Button[] LastButtons = null;
        protected string Refiner = null;
        protected string DefaultProperty = null;

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
            IEnumerable<string> refiners = null)
            : base(new LuisService(new LuisModelAttribute(model, key)))
        {
            SetField.NotNull(out this.Prompts, nameof(Prompts), prompts);
            SetField.NotNull(out this.SearchClient, nameof(searchClient), searchClient);
            this.QueryBuilder = queryBuilder ?? new SearchQueryBuilder();
            this.LastQueryBuilder = new SearchQueryBuilder();
            this.HitStyler = searchHitStyler ?? new SearchHitStyler();
            this.PromptStyler = promptStyler ?? new PromptStyler();
            this.MultipleSelection = multipleSelection;
            if (refiners == null)
            {
                var defaultRefiners = new List<string>();
                foreach (var field in this.SearchClient.Schema.Fields.Values)
                {
                    if (field.IsFacetable && field.NameSynonyms.Alternatives.Any())
                    {
                        defaultRefiners.Add(field.Name);
                    }
                }
                refiners = defaultRefiners;
            }
            var buttons = new List<Button>();
            foreach (var refiner in refiners)
            {
                var field = this.SearchClient.Schema.Field(refiner);
                buttons.Add(new Button(field.Description()));
            }
            Refiners = buttons.ToArray();
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
            await PromptAsync(context, this.Prompts.InitialPrompt, this.Prompts.Browse, this.Prompts.Quit);
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
            await PromptAsync(context, this.Prompts.NotUnderstoodPrompt, LastButtons);
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
                var field = SearchClient.Schema.Field(this.Refiner);
                var desc = field.Description();
                var anyButton = new Button(
                   string.Format(field.Type.IsNumeric() ? this.Prompts.AnyNumberLabel : this.Prompts.AnyLabel, desc),
                   string.Format(this.Prompts.AnyMessage, desc));
                if (field.FilterPreference == PreferredFilter.Facet
                    || field.FilterPreference == PreferredFilter.MinValue
                    || field.FilterPreference == PreferredFilter.MaxValue)
                {
                    var search = await this.ExecuteSearchAsync(this.Refiner);
                    var buttons = new List<Button>();
                    var choices = (from facet in search.Facets[this.Refiner] orderby facet.Value ascending select facet);
                    if (field.FilterPreference == PreferredFilter.Facet)
                    {
                        foreach (var choice in choices)
                        {
                            buttons.Add(new Button(field.ValueSynonyms.Any() ? $"{choice.Value}" : $"{choice.Value} {desc}", $"{choice.Value} ({choice.Count})"));
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
                        buttons.Add(anyButton);
                        await PromptAsync(context, string.Format(Prompts.FacetValuePrompt, desc), buttons.ToArray());
                        context.Wait(MessageReceived);
                    }
                    else
                    {
                        await PromptAsync(context, string.Format(this.Prompts.NoValuesPrompt, desc));
                        context.Wait(MessageReceived);
                    }
                }
                else
                {
                    await PromptAsync(context, string.Format(this.Prompts.FilterPrompt, desc), anyButton);
                    DefaultProperty = this.Refiner;
                    context.Wait(MessageReceived);
                }
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
            var entities = result.Entities ?? new List<EntityRecommendation>();
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
                         let range = Resolve(comparison, result.Query)
                         where range != null
                         select range;
            var filter = GenerateFilterExpression(ranges, Operator.And);
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
            DefaultProperty = null;
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
            await this.PromptAsync(context, this.Prompts.FacetPrompt, this.Refiners);
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

        private Range Resolve(ComparisonEntity c, string originalText)
        {
            Range range = null;
            var propertyName = (c.Property == null ? (DefaultProperty ?? this.SearchClient.Schema.DefaultNumericProperty) : this.FieldCanonicalizer.Canonicalize(c.Property.Entity));
            if (propertyName != null)
            {
                range = new Range { Property = this.SearchClient.Schema.Field(propertyName) };
                bool isCurrency;
                object lower = c.Lower == null ? double.NegativeInfinity : ParseNumber(c.Lower.Entity, out isCurrency);
                object upper = c.Upper == null ? double.PositiveInfinity : ParseNumber(c.Upper.Entity, out isCurrency);
                if (lower is double && double.IsNaN((double)lower))
                {
                    lower = originalText.Substring(c.Lower.StartIndex.Value, c.Lower.EndIndex.Value - c.Lower.StartIndex.Value + 1);
                }
                if (upper is double && double.IsNaN((double)upper))
                {
                    upper = originalText.Substring(c.Upper.StartIndex.Value, c.Upper.EndIndex.Value - c.Upper.StartIndex.Value + 1);
                }
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
                    var dir = sort.Direction == SortDirection.Ascending ? this.Prompts.Ascending : this.Prompts.Descending;
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
            string prompt = this.Prompts.RefinePrompt;
            if (response.Results.Count() == 0)
            {
                this.QueryBuilder = this.LastQueryBuilder;
                response = await this.ExecuteSearchAsync();
                prompt = this.Prompts.NoResultsPrompt;
            }
            var message = context.MakeMessage();
            this.Found = response.Results.ToList();
            this.HitStyler.Show(
                ref message,
                (IReadOnlyList<SearchHit>)this.Found,
                SearchDescription(),
                this.Prompts.Add
                );
            await context.PostAsync(message);
            await PromptAsync(context, prompt, this.Prompts.Browse, this.Prompts.NextPage, this.Prompts.List, this.Prompts.Finished, this.Prompts.Quit, this.Prompts.StartOver);
            this.LastQueryBuilder = this.QueryBuilder;
            context.Wait(MessageReceived);
        }

        protected FilterExpression GenerateFilterExpression(IEnumerable<Range> ranges, Operator connector, FilterExpression soFar = null)
        {
            FilterExpression filter = soFar;
            foreach (var range in ranges)
            {
                var lowercmp = (range.IncludeLower ? Operator.GreaterThanOrEqual : Operator.GreaterThan);
                var uppercmp = (range.IncludeUpper ? Operator.LessThanOrEqual : Operator.LessThan);
                if (range.Lower is double && double.IsNegativeInfinity((double)range.Lower))
                {
                    if (!double.IsPositiveInfinity((double)range.Upper))
                    {
                        filter = FilterExpression.Combine(filter, new FilterExpression(uppercmp, range.Property, range.Upper), connector);
                    }
                }
                else if (range.Upper is double && double.IsPositiveInfinity((double)range.Upper))
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(lowercmp, range.Property, range.Lower), connector);
                }
                else if (range.Lower == range.Upper)
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(range.Lower is string && range.Property.IsSearchable ? Operator.FullText : Operator.Equal, range.Property, range.Lower), connector);
                }
                else
                {
                    var child = FilterExpression.Combine(new FilterExpression(lowercmp, range.Property, range.Lower),
                                        new FilterExpression(uppercmp, range.Property, range.Upper), Operator.And);
                    filter = FilterExpression.Combine(filter, child, connector);
                }
            }
            return filter;
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
    public static partial class Extensions
    {
        public static IEnumerable<string> UncoveredSubstrings(this IEnumerable<EntityRecommendation> entities, string originalText)
        {
            var ranges = new[] { new { start = 0, end = originalText.Length } }.ToList();
            foreach (var entity in entities)
            {
                if (entity.StartIndex.HasValue)
                {
                    int i = 0;
                    while (i < ranges.Count)
                    {
                        var range = ranges[i];
                        if (range.start > entity.EndIndex)
                        {
                            break;
                        }
                        if (range.start == entity.StartIndex)
                        {
                            if (range.end <= entity.EndIndex)
                            {
                                // Completely contained 
                                ranges.RemoveAt(i);
                            }
                            else
                            {
                                // Remove from start
                                ranges.RemoveAt(i);
                                ranges.Insert(i, new { start = entity.EndIndex.Value + 1, end = range.end });
                                ++i;
                            }
                        }
                        else if (range.end == entity.EndIndex)
                        {
                            // Remove from end
                            ranges.RemoveAt(i);
                            ranges.Insert(i, new { start = range.start, end = entity.StartIndex.Value });
                            ++i;
                        }
                        else if (range.start < entity.StartIndex && range.end > entity.EndIndex)
                        {
                            // Split
                            ranges.RemoveAt(i);
                            ranges.Insert(i, new { start = range.start, end = entity.StartIndex.Value });
                            ranges.Insert(++i, new { start = entity.EndIndex.Value + 1, end = range.end });
                            ++i;
                        }
                        else if (range.start > entity.StartIndex && range.end < entity.EndIndex)
                        {
                            // Completely contained
                            ranges.RemoveAt(i);
                        }
                        else
                        {
                            ++i;
                        }
                    }
                }
            }
            var substrings = new List<string>();
            foreach (var range in ranges)
            {
                var str = originalText.Substring(range.start, range.end - range.start).Trim();
                if (!string.IsNullOrEmpty(str))
                {
                    substrings.Add(str);
                }
            }
            return substrings;
        }

        public static FilterExpression GenerateFilterExpression(this IEnumerable<FilterExpression> filters, Operator connector = Operator.And, FilterExpression soFar = null)
        {
            FilterExpression result = soFar;
            foreach (var filter in filters)
            {
                result = FilterExpression.Combine(result, filter, connector);
            }
            return result;
        }
    }
}
