namespace Search.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Models;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class Range
    {
        public SearchField Property;
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

        public int HitsPerPage { get; set; } = DefaultHitPerPage;

        protected SearchSchema schema;
        protected Canonicalizer fieldCanonicalizer;
        protected Dictionary<string, Canonicalizer> valueCanonicalizers;

        public SearchLanguageDialog(SearchSchema searchSchema, string subscription, string appID)
            : base(new LuisService(new LuisModelAttribute(appID, subscription)))
        {
            schema = searchSchema;
            fieldCanonicalizer = new Canonicalizer();
            valueCanonicalizers = new Dictionary<string, Canonicalizer>();
            foreach (var field in schema.Fields.Values)
            {
                fieldCanonicalizer.Add(field.NameSynonyms);
                valueCanonicalizers[field.Name] = new Canonicalizer(field.ValueSynonyms);
            }
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
            // TODO: Remove this
            using (var stream = new System.IO.StreamWriter(@"c:\tmp\luis.json"))
            {
                stream.Write(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }

            var comparisons = (from entity in result.Entities
                               where entity.Type == "Comparison"
                               select new ComparisonEntity(entity)).ToList();
            var attributes = (from entity in result.Entities
                              where schema.Fields.ContainsKey(entity.Type)
                              select new FilterExpression(Operator.Equal, schema.Field(entity.Type),
                                this.valueCanonicalizers[entity.Type].Canonicalize(entity.Entity)));
            var substrings = result.Entities.UncoveredSubstrings(result.Query);
            foreach (var entity in result.Entities)
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
            var spec = new SearchSpec { Text = string.Join(" ", substrings), Filter = filter };
            context.Done(spec);
            return Task.CompletedTask;
        }

        [LuisIntent("NextPage")]
        public Task NextPage(IDialogContext context, LuisResult result)
        {
            return Task.CompletedTask;
        }

        [LuisIntent("Refine")]
        public Task Refine(IDialogContext context, LuisResult result)
        {
            return Task.CompletedTask;
        }

        [LuisIntent("List")]
        public Task List(IDialogContext context, LuisResult result)
        {
            return Task.CompletedTask;
        }

        [LuisIntent("StartOver")]
        public Task StartOver(IDialogContext context, LuisResult result)
        {
            return Task.CompletedTask;
        }

        [LuisIntent("Quit")]
        public Task Quit(IDialogContext context, LuisResult result)
        {
            return Task.CompletedTask;
        }

        [LuisIntent("Done")]
        public Task Done(IDialogContext context, LuisResult result)
        {
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
            return double.Parse(entity) * multiply;
        }

        private Range Resolve(ComparisonEntity c)
        {
            Range range = null;
            var propertyName = this.fieldCanonicalizer.Canonicalize(c.Property?.Entity);
            if (propertyName != null)
            {
                range = new Range { Property = schema.Field(propertyName) };
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
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            upper = double.PositiveInfinity;
                            break;

                        case ">":
                        case "greater than":
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
                        default: throw new ArgumentException($"Unknown operator {c.Operator.Entity}");
                    }
                }
                range.Lower = lower;
                range.Upper = upper;
            }
            return range;
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

        public static FilterExpression GenerateFilterExpression(this IEnumerable<Range> ranges, Operator connector, FilterExpression soFar = null)
        {
            FilterExpression filter = soFar;

            foreach (var range in ranges)
            {
                var lowercmp = (range.IncludeLower ? Operator.GreaterThanOrEqual : Operator.GreaterThan);
                var uppercmp = (range.IncludeUpper ? Operator.LessThanOrEqual : Operator.LessThan);
                if (double.IsNegativeInfinity(range.Lower))
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(uppercmp, range.Property, range.Upper), connector);
                }
                else if (double.IsPositiveInfinity(range.Upper))
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(lowercmp, range.Property, range.Lower), connector);
                }
                else if (range.Lower == range.Upper)
                {
                    filter = FilterExpression.Combine(filter, new FilterExpression(Operator.Equal, range.Property, range.Lower), connector);
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
