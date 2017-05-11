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
using Microsoft.Bot.Builder.FormFlow.Advanced;

namespace Search.Dialogs
{
    // TODO: List of things still to do
    // Switch to built-in currency and numbers 
    // Add support around locations
    // Cannot handle street in RealEstate because of the way facet values are handled.
    // Allow multiple synonyms in canonicalizer and generate a disjunction for query

    public delegate SearchField CanonicalizerDelegate(string propertyName);

    [Serializable]
    public class SearchDialog : LuisDialog<IList<SearchHit>>
    {
        #region Members

        private IList<SearchHit> Found;

        protected readonly ISearchHitStyler HitStyler;
        protected readonly bool MultipleSelection;
        protected readonly Prompts Prompts;
        protected readonly PromptStyler PromptStyler;
        protected Button[] Refiners;
        protected readonly ISearchClient SearchClient;

        protected string DefaultProperty = null;
        protected Button[] LastButtons = null;
        protected SearchSpec LastQuery;
        protected SearchSpec Query;
        protected string Refiner = null;
        protected bool SkipIntro = false;

        [NonSerialized]
        protected Dictionary<string, CanonicalValue> ValueCanonicalizers;

        #endregion Members

        #region Properties

        // Expose this publically so that other dialogs could interact with partial results
        public IList<SearchHit> Selected { get; } = new List<SearchHit>();

        #endregion Properties

        #region Constructor

        public SearchDialog(Prompts prompts, ISearchClient searchClient,
            LuisModelAttribute luis,
            SearchSpec query = null,
            PromptStyler promptStyler = null,
            ISearchHitStyler searchHitStyler = null,
            bool multipleSelection = false,
            IEnumerable<string> refiners = null)
            : base(new LuisService(luis))
        {
            Microsoft.Bot.Builder.Internals.Fibers.SetField.NotNull(out Prompts, nameof(Prompts), prompts);
            Microsoft.Bot.Builder.Internals.Fibers.SetField.NotNull(out SearchClient, nameof(searchClient), searchClient);
            SkipIntro = query != null && !query.HasNoConstraints;
            Query = query ?? new SearchSpec();
            LastQuery = new SearchSpec();
            Query.GetTotalCount = prompts.Count != null;
            HitStyler = searchHitStyler ?? new SearchHitStyler();
            PromptStyler = promptStyler ?? new SearchPromptStyler();
            MultipleSelection = multipleSelection;
            InitializeRefiners(refiners);
        }

        #endregion Constructor

        #region Bot Flow

        protected async Task PromptAsync(IDialogContext context, string prompt, params Button[] buttons)
        {
            var msg = context.MakeMessage();
            PromptStyler.Apply(ref msg, prompt, (from button in buttons select button.Message).ToList(),
                (from button in buttons select button.Label).ToList());
            LastButtons = buttons;
            await context.PostAsync(msg);
        }

        public override async Task StartAsync(IDialogContext context)
        {
            if (SkipIntro)
            {
                await Search(context);
            }
            else
            {
                await PromptAsync(context, Prompts.InitialPrompt, Prompts.Refine, Prompts.Quit);
                context.Wait(MessageReceived);
            }
        }

        protected override IntentRecommendation BestIntentFrom(LuisResult result)
        {
            var best = (from intent in result.Intents
                        let score = intent.Score ?? 0.0
                        where score > 0.3
                        orderby score descending
                        select intent).FirstOrDefault();
            if (best == null || best.Intent == "None")
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

        #endregion Bot Flow

        #region Luis Intents

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
            if (result.Entities.Count() == 1 && result.Entities.First().Type == "KeywordFacet")
            {
                await RemoveKeywords(context);
            }
            else
            {
                var property = result.Entities.First((e) => e.Type == "Properties");
                Refiner = property?.FirstResolution();
                if (Refiner == null)
                {
                    await Filter(context, result);
                }
                else
                {
                    var field = SearchClient.Schema.Field(Refiner);
                    var desc = field.Description();
                    var anyButton = new Button(
                        string.Format(Prompts.AnyMessage, desc),
                        string.Format(field.Type.IsNumeric() ? Prompts.AnyNumberLabel : Prompts.AnyLabel, desc));
                    var preference = field.FilterPreference;
                    if (preference == PreferredFilter.Facet
                        || preference == PreferredFilter.MinValue
                        || preference == PreferredFilter.MaxValue)
                    {
                        var search = await ExecuteSearchAsync(Refiner);
                        var choices = from facet in search.Facets[Refiner]
                                      let facetDesc = FacetDisplay.Describe(facet, ValueCanonicalizers)
                                      orderby facetDesc ascending
                                      select new GenericFacet() { Value = facetDesc, Count = facet.Count };

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
        }

        // Return all top-level entities
        private IEnumerable<EntityRecommendation> TopEntities(IEnumerable<EntityRecommendation> entities)
        {
            var sorted = (from entity in entities orderby entity.StartIndex, entity.EndIndex descending select entity).ToArray();
            if (sorted.Any())
            {
                var root = sorted.First();
                foreach (var child in sorted)
                {
                    if (child.StartIndex > root.EndIndex 
                        || (child != root && child.StartIndex == root.StartIndex && child.EndIndex == root.EndIndex))
                    {
                        yield return root;
                        root = child;
                    }
                }
                yield return root;
            }
        }

        private async Task RemoveKeywords(IDialogContext context)
        {
            if (Query.Phrases.Any())
            {
                var buttons = new List<Button>();
                foreach (var phrase in Query.Phrases.OrderBy(k => k))
                {
                    buttons.Add(new Button(phrase, string.Format(Prompts.RemoveKeywordMessage, phrase)));
                }
                await PromptAsync(context, Prompts.AddOrRemoveKeywordPrompt, buttons.ToArray());
            }
            else
            {
                await PromptAsync(context, Prompts.AddKeywordPrompt);
            }
            context.Wait(MessageReceived);
        }

        [LuisIntent("Filter")]
        public async Task Filter(IDialogContext context, LuisResult result)
        {
            Canonicalizers();
            var query = result.AlteredQuery ?? result.Query;
            var entities = result.Entities ?? new List<EntityRecommendation>();
            var nonEntities = Keywords.NonEntityRanges(entities, query.Length).ToList();
            var rangeResolver = new RangeResolver(SearchClient.Schema);
            var topEntities = TopEntities(entities).ToArray();
            var comparisons = (from entity in topEntities
                               where entity.Type == "Comparison"
                               select new ComparisonEntity(entity)).ToList();
            var attributes = from entity in topEntities
                             let canonical = CanonicalAttribute(entity)
                             where canonical != null
                             select new FilterExpression(entity.Entity, FilterOperator.Equal, canonical.Field, canonical.Value);
            var removeProperties = from entity in topEntities where entity.Type == "Removal" select entity;
            var removeKeywords = from entity in topEntities where entity.Type == "RemoveKeyword" select entity;
            var bareRemoves = from entity in topEntities where entity.Type == "RemoveKeywords" select entity;

            // Remove property constraints
            foreach (var removal in removeProperties)
            {
                foreach (var entity in entities)
                {
                    if (entity.Type == "Properties"
                        && entity.StartIndex >= removal.StartIndex
                        && entity.EndIndex <= removal.EndIndex)
                    {
                        if (Query.Filter != null)
                        {
                            Query.Filter =
                                Query.Filter.Remove(SearchClient.Schema.Field(entity.FirstResolution()));
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            var removePhrases = new List<string>();
            var removeRanges = new List<Keywords.Range>();
            foreach (var remove in bareRemoves)
            {
                foreach (var nonEntity in nonEntities)
                {
                    if (nonEntity.Start - 1 == remove.EndIndex)
                    {
                        removeRanges.Add(nonEntity);
                        break;
                    }
                    else if (nonEntity.Start > remove.EndIndex)
                    {
                        break;
                    }
                }
            }
            foreach (var range in removeRanges)
            {
                removePhrases.AddRange(query.Substring(range.Start, range.End - range.Start + 1).Phrases());
                nonEntities.Remove(range);
            }

            var addKeywords = Keywords.ExtractPhrases(query, nonEntities).ToList();
            // Bare keywords are keywords
            foreach (var entity in topEntities)
            {
                if (entity.Type == "Keyword")
                {
                    foreach (var phrase in entity.Entity.Phrases())
                    {
                        addKeywords.Add(phrase);
                    }
                }
            }

            if (Query.Phrases != null && (removeKeywords.Any() || removePhrases.Any()))
            {
                // Remove keywords from the filter
                foreach (var removeKeyword in removeKeywords)
                {
                    foreach (var entity in entities)
                    {
                        if (entity.Type == "Keyword"
                            && entity.StartIndex >= removeKeyword.StartIndex
                            && entity.EndIndex <= removeKeyword.EndIndex)
                        {
                            removePhrases.AddRange(entity.Entity.Phrases());
                        }
                    }
                }
                Query.Phrases = Query.Phrases.Except(removePhrases).ToList();
            }

            foreach (var entity in entities)
            {
                foreach (var comparison in comparisons)
                {
                    comparison.AddEntity(entity);
                }
            }

            // Find comparisons with unrecognized words inside
            foreach (var comparison in comparisons)
            {
                if (comparison.Lower == null && comparison.Upper == null && comparison.Property != null
                    && comparison.Property.EndIndex.HasValue && comparison.Entity.EndIndex.HasValue)
                {
                    if (comparison.Property.EndIndex == comparison.Entity.EndIndex)
                    {
                        comparison.Lower = new EntityRecommendation(null, null, comparison.Entity.Entity.Substring(0, comparison.Property.StartIndex.Value));
                    }
                    else if (comparison.Property.StartIndex == comparison.Entity.StartIndex)
                    {
                        comparison.Lower = new EntityRecommendation(null, null, comparison.Entity.Entity.Substring(comparison.Property.EndIndex.Value, comparison.Entity.EndIndex.Value + 1));
                    }
                }
            }

            var ranges = from comparison in comparisons
                         let range = rangeResolver.Resolve(comparison, result.Query, DefaultProperty)
                         where range != null
                         select range;
            var filter = FilterExpressionBuilder.Build(ranges, FilterOperator.And);
            filter = attributes.GenerateFilterExpression(FilterOperator.And, filter);

            if (Query.Filter != null)
            {
                Query.Filter = FilterExpression.Combine(Query.Filter.Remove(filter), filter,
                    FilterOperator.And);
            }
            else
            {
                Query.Filter = filter;
            }
            Query.Phrases = Query.Phrases.Union(addKeywords).ToList();
            DefaultProperty = null;
            await Search(context);
        }

        [LuisIntent("NextPage")]
        public async Task NextPage(IDialogContext context, LuisResult result)
        {
            Query.PageNumber++;
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

        [LuisIntent("StartOver")]
        public async Task StartOver(IDialogContext context, LuisResult result)
        {
            Query = new SearchSpec();
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

        #endregion Luis Intents

        #region Search

        private string SearchDescription(long? totalCount)
        {

            var description = Query.Description(Prompts);
            if (totalCount != null)
            {
                description += Environment.NewLine + string.Format(Prompts.Count, totalCount.Value);
            }
            if (Selected.Any())
            {
                var builder = new StringBuilder();
                builder.AppendLine(string.Format(Prompts.Selected, Selected.Count()));
                builder.AppendLine();
                builder.Append(description);
                description = builder.ToString();
            }
            return description;
        }

        public async Task Search(IDialogContext context)
        {
            var prompt = Prompts.RefinePrompt;
            if (Query.Equals(LastQuery))
            {
                prompt = Prompts.NotUnderstoodPrompt;
            }
            else
            {
                var response = await ExecuteSearchAsync();
                if (!response.Results.Any())
                {
                    var badQuery = Query;
                    Query = LastQuery;
                    // Ensure the queries are not equal
                    LastQuery = badQuery;
                    prompt = string.Format(Prompts.NoResultsPrompt, badQuery.Description(Prompts));
                }
                else
                {
                    var message = context.MakeMessage();
                    Found = response.Results.ToList();
                    HitStyler.Show(
                        ref message,
                        (IReadOnlyList<SearchHit>)Found,
                        SearchDescription(response.TotalCount),
                        Prompts.Add
                    );
                    await context.PostAsync(message);
                    LastQuery = Query.DeepCopy();
                    LastQuery.PageNumber = 0;
                }
            }
            await
                PromptAsync(context, prompt, Prompts.Refine, Prompts.NextPage, Prompts.List, Prompts.Finished,
                    Prompts.Quit, Prompts.StartOver);
            context.Wait(MessageReceived);
        }

        protected async Task<GenericSearchResult> ExecuteSearchAsync(string facet = null)
        {
            return await SearchClient.SearchAsync(Query, facet);
        }

        #endregion Search

        #region User List

        protected virtual async Task AddSelectedItem(IDialogContext context, string selection)
        {
            var hit = Found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, Prompts.UnknownItemPrompt, LastButtons);
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
                    await PromptAsync(context, string.Format(Prompts.AddedToListPrompt, hit.Title), LastButtons);
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
            var hit = Selected.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await PromptAsync(context, Prompts.UnknownItemPrompt, LastButtons);
                context.Wait(MessageReceived);
            }
            else
            {
                await PromptAsync(context, string.Format(Prompts.RemovedFromListPrompt, hit.Title));
                Selected.Remove(hit);
                if (Selected.Any())
                {
                    await ShowList(context);
                }
                else
                {
                    await PromptAsync(context, Prompts.RefinePrompt, LastButtons);
                }
            }
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
                HitStyler.Show(ref message, (IReadOnlyList<SearchHit>)Selected, Prompts.ListPrompt, Prompts.Remove);
                await context.PostAsync(message);
                await
                    PromptAsync(context, Prompts.RefinePrompt, Prompts.Finished, Prompts.Quit, Prompts.StartOver,
                        Prompts.Refine);
            }
            context.Wait(MessageReceived);
        }

        #endregion User List

        #region Helpers 

        private void InitializeRefiners(IEnumerable<string> refiners)
        {
            if (refiners == null)
            {
                var defaultRefiners = new List<string>();
                foreach (var field in SearchClient.Schema.Fields.Values.OrderBy(f => f.Name))
                {
                    if (field.IsFacetable && field.NameSynonyms.Alternatives.Any())
                    {
                        defaultRefiners.Add(field.Name);
                    }
                }
                defaultRefiners.Add(Prompts.Keyword.Label);
                refiners = defaultRefiners;
            }
            var buttons = new List<Button>();
            foreach (var refiner in refiners)
            {
                if (refiner == Prompts.Keyword.Label)
                {
                    buttons.Add(Prompts.Keyword);
                }
                else
                {
                    var field = SearchClient.Schema.Field(refiner);
                    buttons.Add(new Button(field.Description()));
                }
            }
            Refiners = buttons.ToArray();
        }

        private void Canonicalizers()
        {
            if (ValueCanonicalizers == null)
            {
                ValueCanonicalizers = new Dictionary<string, CanonicalValue>();
                foreach (var field in SearchClient.Schema.Fields.Values)
                {
                    foreach (var synonym in field.ValueSynonyms)
                    {
                        // TODO: We have two different fields with the same value for now, just pick one
                        if (!ValueCanonicalizers.ContainsKey(synonym.Canonical))
                        {
                            ValueCanonicalizers.Add(synonym.Canonical,
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

        private CanonicalValue CanonicalAttribute(EntityRecommendation entity)
        {
            CanonicalValue canonical = null;
            if (entity.Type == "Attributes")
            {
                var key = entity.FirstResolution();
                // Ignore noise words as attributes
                // TODO: Ideally we would filter when generating, but the Bot Builder is not available in .net core yet
                if (!Language.NoiseWord(key))
                {
                    ValueCanonicalizers.TryGetValue(key, out canonical);
                }
            }
            return canonical;
        }

        #endregion Initialization 
    }

    public class CanonicalValue
    {
        public string Description;
        public SearchField Field;
        public string Value;
    }
}