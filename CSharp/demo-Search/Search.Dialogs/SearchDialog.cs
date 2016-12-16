using Search.Dialogs.Luis;
using Search.Dialogs.Tools;
using Search.Dialogs.UserInteraction;

namespace Search.Dialogs
{
    // TODO: List of things still to do
    // Switch to built-in currency and numbers 
    // Improve the filter description by attaching original phrases to query expressions and showing them
    // Show the total number of matches
    // Add spelling support
    // Provide a way to reset the keywords
    // Add support around locations
    // Cannot handle street in RealEstate because of the way facet values are handled.
    // Allow multiple synonyms in canonicalizer and generate a disjunction for query
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Fibers = Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Search.Models;
    using Search.Services;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Builder.Luis;
    using System.Text;
    using Azure;

    public delegate SearchField CanonicalizerDelegate(string propertyName);

    [Serializable]
    public class SearchDialog : LuisDialog<IList<SearchHit>>
    {
        // Expose this publically so that other dialogs could interact with partial results
        public readonly IList<SearchHit> Selected = new List<SearchHit>();

        protected class CanonicalValue
        {
            public SearchField Field;
            public string Value;
            public string Description;
        }

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

        private IList<SearchHit> Found;

        [NonSerialized]
        protected Canonicalizer FieldCanonicalizer;

        [NonSerialized]
        protected Dictionary<string, CanonicalValue> ValueCanonicalizers;

        public SearchDialog(Prompts prompts, ISearchClient searchClient, string key, string model, SearchQueryBuilder queryBuilder = null,
            PromptStyler promptStyler = null,
            ISearchHitStyler searchHitStyler = null,
            bool multipleSelection = false,
            IEnumerable<string> refiners = null)
            : base(new LuisService(new LuisModelAttribute(model, key)))
        {
            Fibers.SetField.NotNull(out this.Prompts, nameof(Prompts), prompts);
            Fibers.SetField.NotNull(out this.SearchClient, nameof(searchClient), searchClient);
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

        private string FacetDescription(GenericFacet facet)
        {
            string description;
            if (facet.Value is string)
            {
                description = (string)facet.Value;
                CanonicalValue value;
                if (ValueCanonicalizers.TryGetValue(description, out value))
                {
                    description = value.Description;
                }
            }
            else
            {
                description = facet.Value.ToString();
            }
            return description;
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
                var preference = field.FilterPreference;
                if (preference == PreferredFilter.Facet
                    || preference == PreferredFilter.MinValue
                    || preference == PreferredFilter.MaxValue)
                {
                    var search = await this.ExecuteSearchAsync(this.Refiner);
                    var choices = (from facet in search.Facets[this.Refiner]
                                   let facetDesc = FacetDescription(facet)
                                   orderby facetDesc ascending
                                   select new GenericFacet() { Value = facetDesc, Count = facet.Count });
                    var buttons = new List<Button>();
                    if (preference == PreferredFilter.Facet)
                    {
                        foreach (var choice in choices)
                        {
                            buttons.Add(new Button(field.ValueSynonyms.Any() ? $"{choice.Value}" : $"{choice.Value} {desc}", $"{choice.Value} ({choice.Count})"));
                        }
                    }
                    else if (preference == PreferredFilter.MinValue)
                    {
                        var total = choices.Sum((choice) => choice.Count);
                        foreach (var choice in choices)
                        {
                            buttons.Add(new Button($"{choice.Value}+ {desc}", $"{choice.Value}+ ({total})"));
                            total -= choice.Count;
                        }
                    }
                    else if (preference == PreferredFilter.MaxValue)
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
                ValueCanonicalizers = new Dictionary<string, CanonicalValue>();
                foreach (var field in this.SearchClient.Schema.Fields.Values)
                {
                    FieldCanonicalizer.Add(field.NameSynonyms);
                    foreach (var synonym in field.ValueSynonyms)
                    {
                        foreach (var alt in synonym.Alternatives)
                        {
                            ValueCanonicalizers.Add(Normalize(alt), new CanonicalValue { Field = field, Value = synonym.Canonical, Description = synonym.Description });
                        }
                    }
                }
            }
        }

        private string Normalize(string source)
        {
            return source.Trim().ToLower();
        }

        private CanonicalValue CanonicalAttribute(dynamic entity)
        {
            CanonicalValue canonical = null;
            if (entity.Type != "Attribute" || !ValueCanonicalizers.TryGetValue(Normalize(entity.Entity), out canonical))
            {
                canonical = null;
            }
            return canonical;
        }

        [LuisIntent("Filter")]
        public async Task Filter(IDialogContext context, LuisResult result)
        {
            Canonicalizers();
            var rangeResolver = new RangeResolver(this.SearchClient.Schema, FieldCanonicalizer);

            var entities = result.Entities ?? new List<EntityRecommendation>();
            var comparisons = (from entity in entities
                               where entity.Type == "Comparison"
                               select new ComparisonEntity(entity)).ToList();
            var attributes = (from entity in entities
                              let canonical = CanonicalAttribute(entity)
                              where canonical != null
                              select new FilterExpression(entity.Entity, Operator.Equal, canonical.Field, canonical.Value));
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
                         let range = rangeResolver.Resolve(comparison, result.Query, this.DefaultProperty)
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
            this.QueryBuilder.Spec.Phrases = this.QueryBuilder.Spec.Phrases.Union(substrings).ToList();
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
            context.Done(Selected);
            return Task.CompletedTask;
        }

        private string SearchDescription()
        {
            var builder = new StringBuilder();
            var filter = this.QueryBuilder.Spec.Filter;
            var phrases = this.QueryBuilder.Spec.Phrases;
            var sorts = this.QueryBuilder.Spec.Sort;
            if (this.Selected.Any())
            {
                builder.AppendLine(string.Format(this.Prompts.Selected, Selected.Count()));
                builder.AppendLine();
            }
            if (filter != null)
            {
                builder.AppendLine(string.Format(this.Prompts.Filter, filter.ToUserFriendlyString()));
                builder.AppendLine();
            }
            if (phrases.Any())
            {
                var phraseBuilder = new StringBuilder();
                var prefix = "";
                foreach (var phrase in phrases)
                {
                    phraseBuilder.Append($"{prefix}\"{phrase}\"");
                    prefix = " ";
                }
                builder.AppendLine(string.Format(this.Prompts.Keywords, phraseBuilder.ToString()));
                builder.AppendLine();
            }
            if (sorts.Any())
            {
                var sortBuilder = new StringBuilder();
                var prefix = "";
                foreach (var sort in sorts)
                {
                    var dir = sort.Direction == SortDirection.Ascending ? this.Prompts.Ascending : this.Prompts.Descending;
                    sortBuilder.Append($"{prefix}{sort.Field} {dir}");
                    prefix = ", ";
                }
                builder.AppendLine(string.Format(this.Prompts.Sort, sortBuilder.ToString()));
                builder.AppendLine();
            }
            builder.AppendLine(string.Format(this.Prompts.Page, this.QueryBuilder.PageNumber));
            builder.AppendLine();
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
            this.LastQueryBuilder = this.QueryBuilder.DeepCopy();
            this.LastQueryBuilder.PageNumber = 0;
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
                    if (range.Upper is double && !double.IsPositiveInfinity((double)range.Upper))
                    {
                        filter = FilterExpression.Combine(filter, new FilterExpression(range.Description, uppercmp, range.Property, range.Upper), connector);
                    }
                }
                else if (range.Upper is double && double.IsPositiveInfinity((double)range.Upper))
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(range.Description, lowercmp, range.Property, range.Lower), connector);
                }
                else if (range.Lower == range.Upper)
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(range.Description, range.Lower is string && range.Property.IsSearchable ? Operator.FullText : Operator.Equal, range.Property, range.Lower), connector);
                }
                else
                {
                    //Only add the description to the combination to avoid description duplication and limit the tree traversal
                    var child = FilterExpression.Combine(new FilterExpression(lowercmp, range.Property, range.Lower),
                                        new FilterExpression(uppercmp, range.Property, range.Upper), Operator.And, range.Description);
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
}
