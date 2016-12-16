using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using Search.Azure;
using Search.Dialogs.Filter;
using Search.Dialogs.Tools;
using Search.Dialogs.UserInteraction;
using Search.Models;
using Search.Services;

namespace Search.Dialogs
{
    // TODO: List of things still to do
    // Switch to built-in currency and numbers 
    // Show the total number of matches
    // Add spelling support
    // Provide a way to reset the keywords
    // Add support around locations
    // Cannot handle street in RealEstate because of the way facet values are handled.
    // Allow multiple synonyms in canonicalizer and generate a disjunction for query
    
    public delegate SearchField CanonicalizerDelegate(string propertyName);

    [Serializable]
    public class SearchDialog : LuisDialog<IList<SearchHit>>
    {
        protected readonly ISearchHitStyler HitStyler;
        protected readonly bool MultipleSelection;

        protected readonly Prompts Prompts;
        protected readonly PromptStyler PromptStyler;
        protected readonly Button[] Refiners;
        protected readonly ISearchClient SearchClient;
        // Expose this publically so that other dialogs could interact with partial results
        public readonly IList<SearchHit> Selected = new List<SearchHit>();
        protected string DefaultProperty = null;

        [NonSerialized] protected Canonicalizer FieldCanonicalizer;

        private IList<SearchHit> Found;
        protected Button[] LastButtons = null;
        protected SearchQueryBuilder LastQueryBuilder;
        protected SearchQueryBuilder QueryBuilder;
        protected string Refiner = null;

        [NonSerialized] protected Dictionary<string, CanonicalValue> ValueCanonicalizers;

        public SearchDialog(Prompts prompts, ISearchClient searchClient, string key, string model,
            SearchQueryBuilder queryBuilder = null,
            PromptStyler promptStyler = null,
            ISearchHitStyler searchHitStyler = null,
            bool multipleSelection = false,
            IEnumerable<string> refiners = null)
            : base(new LuisService(new LuisModelAttribute(model, key)))
        {
            Microsoft.Bot.Builder.Internals.Fibers.SetField.NotNull(out Prompts, nameof(Prompts), prompts);
            Microsoft.Bot.Builder.Internals.Fibers.SetField.NotNull(out SearchClient, nameof(searchClient), searchClient);
            QueryBuilder = queryBuilder ?? new SearchQueryBuilder();
            LastQueryBuilder = new SearchQueryBuilder();
            HitStyler = searchHitStyler ?? new SearchHitStyler();
            PromptStyler = promptStyler ?? new PromptStyler();
            MultipleSelection = multipleSelection;
            if (refiners == null)
            {
                var defaultRefiners = new List<string>();
                foreach (var field in SearchClient.Schema.Fields.Values)
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
                var field = SearchClient.Schema.Field(refiner);
                buttons.Add(new Button(field.Description()));
            }
            Refiners = buttons.ToArray();
        }

        protected async Task PromptAsync(IDialogContext context, string prompt, params Button[] buttons)
        {
            var msg = context.MakeMessage();
            PromptStyler.Apply(ref msg, prompt, (from button in buttons select button.Label).ToList(),
                (from button in buttons select button.Message).ToList());
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
                await AddSelectedItem(context, id);
            }
            else if (text.StartsWith("REMOVE:"))
            {
                var id = text.Substring(7).Trim();
                await RemoveSelectedItem(context, id);
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
            Refiner = FieldCanonicalizer.Canonicalize(result.Query);
            if (Refiner == null)
            {
                await Filter(context, result);
            }
            else
            {
                var field = SearchClient.Schema.Field(Refiner);
                var desc = field.Description();
                var anyButton = new Button(
                    string.Format(field.Type.IsNumeric() ? Prompts.AnyNumberLabel : Prompts.AnyLabel, desc),
                    string.Format(Prompts.AnyMessage, desc));
                var preference = field.FilterPreference;
                if (preference == PreferredFilter.Facet
                    || preference == PreferredFilter.MinValue
                    || preference == PreferredFilter.MaxValue)
                {
                    var search = await ExecuteSearchAsync(Refiner);
                    var choices = from facet in search.Facets[Refiner]
                        let facetDesc = FacetDisplay.Describe(facet, ValueCanonicalizers)
                        orderby facetDesc ascending
                        select new GenericFacet() {Value = facetDesc, Count = facet.Count};

                    var buttons = FacetDisplay.Buttons(preference, choices, field, desc);

                    if (buttons.Any())
                    {
                        buttons.Add(anyButton);
                        await PromptAsync(context, string.Format(Prompts.FacetValuePrompt, desc), buttons.ToArray());
                        context.Wait(MessageReceived);
                    }
                    else
                    {
                        await PromptAsync(context, string.Format(Prompts.NoValuesPrompt, desc));
                        context.Wait(MessageReceived);
                    }
                }
                else
                {
                    await PromptAsync(context, string.Format(Prompts.FilterPrompt, desc), anyButton);
                    DefaultProperty = Refiner;
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
                foreach (var field in SearchClient.Schema.Fields.Values)
                {
                    FieldCanonicalizer.Add(field.NameSynonyms);
                    foreach (var synonym in field.ValueSynonyms)
                    {
                        foreach (var alt in synonym.Alternatives)
                        {
                            ValueCanonicalizers.Add(Normalize(alt),
                                new CanonicalValue
                                {
                                    Field = field,
                                    Value = synonym.Canonical,
                                    Description = synonym.Description
                                });
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
            var rangeResolver = new RangeResolver(SearchClient.Schema, FieldCanonicalizer);

            var entities = result.Entities ?? new List<EntityRecommendation>();
            var comparisons = (from entity in entities
                where entity.Type == "Comparison"
                select new ComparisonEntity(entity)).ToList();

            var attributes = from entity in entities
                let canonical = CanonicalAttribute(entity)
                where canonical != null
                select new FilterExpression(entity.Entity, Operator.Equal, canonical.Field, canonical.Value);

            var removals = from entity in entities where entity.Type == "Removal" select entity;
            var substrings = Keywords.ExtractPhrases(entities, result.Query);

            foreach (var removal in removals)
            {
                foreach (var entity in entities)
                {
                    if (entity.Type == "Property"
                        && entity.StartIndex >= removal.StartIndex
                        && entity.EndIndex <= removal.EndIndex)
                    {
                        if (QueryBuilder.Spec.Filter != null)
                        {
                            QueryBuilder.Spec.Filter =
                                QueryBuilder.Spec.Filter.Remove(
                                    SearchClient.Schema.Field(FieldCanonicalizer.Canonicalize(entity.Entity)));
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
                let range = rangeResolver.Resolve(comparison, result.Query, DefaultProperty)
                where range != null
                select range;
            var filter = FilterExpressionBuilder.Build(ranges, Operator.And);
            filter = attributes.GenerateFilterExpression(Operator.And, filter);

            if (QueryBuilder.Spec.Filter != null)
            {
                QueryBuilder.Spec.Filter = FilterExpression.Combine(QueryBuilder.Spec.Filter.Remove(filter), filter,
                    Operator.And);
            }
            else
            {
                QueryBuilder.Spec.Filter = filter;
            }
            QueryBuilder.Spec.Phrases = QueryBuilder.Spec.Phrases.Union(substrings).ToList();
            DefaultProperty = null;
            await Search(context);
        }

        [LuisIntent("NextPage")]
        public async Task NextPage(IDialogContext context, LuisResult result)
        {
            QueryBuilder.PageNumber++;
            await Search(context);
        }

        [LuisIntent("Refine")]
        public async Task Refine(IDialogContext context, LuisResult result)
        {
            await PromptAsync(context, Prompts.FacetPrompt, Refiners);
            context.Wait(MessageReceived);
        }

        [LuisIntent("List")]
        public async Task List(IDialogContext context, LuisResult result)
        {
            await ShowList(context);
        }

        private async Task ShowList(IDialogContext context)
        {
            if (Selected.Count == 0)
            {
                await PromptAsync(context, Prompts.NotAddedPrompt, LastButtons);
            }
            else
            {
                var message = context.MakeMessage();
                HitStyler.Show(ref message, (IReadOnlyList<SearchHit>) Selected, Prompts.ListPrompt, Prompts.Remove);
                await context.PostAsync(message);
                await
                    PromptAsync(context, Prompts.RefinePrompt, Prompts.Finished, Prompts.Quit, Prompts.StartOver,
                        Prompts.Browse);
            }
            context.Wait(MessageReceived);
        }

        [LuisIntent("StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            QueryBuilder.Reset();
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
            var filter = QueryBuilder.Spec.Filter;
            var phrases = QueryBuilder.Spec.Phrases;
            var sorts = QueryBuilder.Spec.Sort;
            if (Selected.Any())
            {
                builder.AppendLine(string.Format(Prompts.Selected, Selected.Count()));
                builder.AppendLine();
            }
            if (filter != null)
            {
                builder.AppendLine(string.Format(Prompts.Filter, filter.ToUserFriendlyString()));
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
                builder.AppendLine(string.Format(Prompts.Keywords, phraseBuilder.ToString()));
                builder.AppendLine();
            }
            if (sorts.Any())
            {
                var sortBuilder = new StringBuilder();
                var prefix = "";
                foreach (var sort in sorts)
                {
                    var dir = sort.Direction == SortDirection.Ascending ? Prompts.Ascending : Prompts.Descending;
                    sortBuilder.Append($"{prefix}{sort.Field} {dir}");
                    prefix = ", ";
                }
                builder.AppendLine(string.Format(Prompts.Sort, sortBuilder.ToString()));
                builder.AppendLine();
            }
            builder.AppendLine(string.Format(Prompts.Page, QueryBuilder.PageNumber));
            builder.AppendLine();
            return builder.ToString();
        }

        public async Task Search(IDialogContext context)
        {
            var response = await ExecuteSearchAsync();
            var prompt = Prompts.RefinePrompt;
            if (!response.Results.Any())
            {
                QueryBuilder = LastQueryBuilder;
                response = await ExecuteSearchAsync();
                prompt = Prompts.NoResultsPrompt;
            }
            var message = context.MakeMessage();
            Found = response.Results.ToList();
            HitStyler.Show(
                ref message,
                (IReadOnlyList<SearchHit>) Found,
                SearchDescription(),
                Prompts.Add
            );
            await context.PostAsync(message);
            await
                PromptAsync(context, prompt, Prompts.Browse, Prompts.NextPage, Prompts.List, Prompts.Finished,
                    Prompts.Quit, Prompts.StartOver);
            LastQueryBuilder = QueryBuilder.DeepCopy();
            LastQueryBuilder.PageNumber = 0;
            context.Wait(MessageReceived);
        }

        protected async Task<GenericSearchResult> ExecuteSearchAsync(string facet = null)
        {
            return await SearchClient.SearchAsync(QueryBuilder, facet);
        }

        protected virtual async Task AddSelectedItem(IDialogContext context, string selection)
        {
            var hit = Found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, Prompts.UnknownItemPrompt);
                context.Wait(MessageReceived);
            }
            else
            {
                if (!Selected.Any(h => h.Key == hit.Key))
                {
                    Selected.Add(hit);
                }

                if (MultipleSelection)
                {
                    await PromptAsync(context, string.Format(Prompts.AddedToListPrompt, hit.Title));
                    context.Wait(MessageReceived);
                }
                else
                {
                    context.Done(Selected);
                }
            }
        }

        protected virtual async Task RemoveSelectedItem(IDialogContext context, string selection)
        {
            var hit = Found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, Prompts.UnknownItemPrompt);
                context.Wait(MessageReceived);
            }
            else
            {
                await PromptAsync(context, string.Format(Prompts.RemovedFromListPrompt, hit.Title));
                Selected.Remove(hit);
                await ShowList(context);
            }
        }
    }

    public class CanonicalValue
    {
        public string Description;
        public SearchField Field;
        public string Value;
    }
}