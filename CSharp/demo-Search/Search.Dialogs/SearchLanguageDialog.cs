
namespace Search.Dialogs
{
    using Microsoft.Azure.Search.Models;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SearchSpec
    {
        public string Text;
        public SearchParameters Parameters;
    }

    public class Range
    {
        public string Property;
        public double Lower;
        public double Upper;
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
                    case "Number": AddNumber(entity); break;
                    case "Dimension": AddNumber(entity); break;
                    case "Operator": Operator = entity; break;
                    case "Property": Property = entity; break;
                }
            }
        }

        public double ParseNumber(string entity, out bool isCurrency)
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
            return double.Parse(entity) * multiply;
        }

        public Range Resolve(CanonicalizerDelegate canonicalizer)
        {
            var comparison = new Range { Property = canonicalizer(Property?.Entity) };
            bool isCurrency;
            var lower = Lower == null ? double.NegativeInfinity : ParseNumber(Lower.Entity, out isCurrency);
            var upper = Upper == null ? double.PositiveInfinity : ParseNumber(Upper.Entity, out isCurrency);
            switch (Operator.Entity)
            {
                case ">=":
                case "+":
                case "greater than or equal":
                case "at least":
                    comparison.IncludeLower = true;
                    comparison.IncludeUpper = true;
                    upper = double.PositiveInfinity;
                    break;

                case ">":
                case "greater than":
                    comparison.IncludeLower = false;
                    comparison.IncludeUpper = true;
                    upper = double.PositiveInfinity;
                    break;

                case "-":
                case "between":
                case "and":
                case "or":
                    comparison.IncludeLower = true;
                    comparison.IncludeUpper = true;
                    break;

                case "<=":
                case "no more than":
                case "less than or equal":
                    comparison.IncludeLower = true;
                    comparison.IncludeUpper = true;
                    upper = lower;
                    lower = double.NegativeInfinity;
                    break;

                case "<":
                case "less than":
                    comparison.IncludeLower = true;
                    comparison.IncludeUpper = false;
                    upper = lower;
                    lower = double.NegativeInfinity;
                    break;

                // This is the case where we just have naked values
                case "":
                    comparison.IncludeLower = true;
                    comparison.IncludeUpper = true;
                    upper = lower;
                    break;

                default: throw new ArgumentException($"Unknown operator {Operator.Entity}");
            }
            comparison.Lower = lower;
            comparison.Upper = upper;
            return comparison;
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
    public class SearchLanguageDialog : LuisDialog<SearchSpec>
    {
        private const int DefaultHitPerPage = 5;

        public string SearchText { get; set; }

        public int PageNumber { get; set; }

        public int HitsPerPage { get; set; } = DefaultHitPerPage;

        protected SearchSchema schema;

        public SearchLanguageDialog(SearchSchema searchSchema, string subscription, string appID)
            : base(new LuisService(new LuisModelAttribute(appID, subscription)))
        {
            schema = searchSchema;
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("I'm sorry. I didn't understand you.");
            context.Wait(MessageReceived);
        }

        [LuisIntent("Filter")]
        public Task ProcessComparison(IDialogContext context, LuisResult result)
        {
            var comparisons = (from entity in result.Entities where entity.Type == "Comparison" select new ComparisonEntity(entity)).ToList();
            var attributes = (from entity in result.Entities where schema.Fields.ContainsKey(entity.Type) select entity);
            var substrings = result.Entities.UncoveredSubstrings(result.Query);
            foreach (var entity in result.Entities)
            {
                foreach (var comparison in comparisons)
                {
                    comparison.AddEntity(entity);
                }
            }
            var ranges = from comparison in comparisons select comparison.Resolve(schema.CanonicalProperty);
            var filter = ranges.GenerateFilter();
            var spec = new SearchSpec { Parameters = new SearchParameters { Filter = filter } };
            context.Done(spec);
            return Task.CompletedTask;
        }
    }

    public static partial class Extensions
    {
        public static IEnumerable<string> UncoveredSubstrings(this IEnumerable<EntityRecommendation> entities, string originalText)
        {
            var ranges = new[] { new { start = 0, end = originalText.Length } }.ToList();
            foreach(var entity in entities)
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
                            ranges.Insert(i, new { start = range.start, end = entity.StartIndex.Value - 1 });
                            ++i;
                        }
                        else if (range.start < entity.StartIndex && range.end > entity.EndIndex)
                        {
                            // Split
                            ranges.RemoveAt(i);
                            ranges.Insert(i, new { start = range.start, end = entity.StartIndex.Value - 1 });
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
            foreach(var range in ranges)
            {
                substrings.Add(originalText.Substring(range.start, range.end - range.start));
            }
            return substrings;
        }

        public static string GenerateFilter(this IEnumerable<Range> ranges)
        {
            var filter = new StringBuilder();
            var seperator = "";
            foreach (var range in ranges)
            {
                filter.Append($"{seperator}");
                var lowercmp = (range.IncludeLower ? "ge" : "gt");
                var uppercmp = (range.IncludeUpper ? "le" : "lt");
                if (double.IsNegativeInfinity(range.Lower))
                {
                    filter.Append($"{range.Property} {uppercmp} {range.Upper}");
                }
                else if (double.IsPositiveInfinity(range.Upper))
                {
                    filter.Append($"{range.Property} {lowercmp} {range.Lower}");
                }
                else if (range.Lower == range.Upper)
                {
                    filter.Append($"{range.Property} eq {range.Lower}");
                }
                else
                {
                    filter.Append($"({range.Property} {lowercmp} {range.Lower} and {range.Property} {uppercmp} {range.Upper})");
                }
                seperator = " and ";
            }
            return filter.ToString();
        }
    }
}
