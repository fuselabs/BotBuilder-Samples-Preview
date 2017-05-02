using Microsoft.Bot.Builder.Luis.Models;
using Newtonsoft.Json.Linq;
using Search.Dialogs.UserInteraction;
using Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Dialogs
{
    public static partial class Extensions
    {
        public static string Description(this SearchSpec spec, Prompts prompts)
        {
            var builder = new StringBuilder();
            var filter = spec.Filter;
            var phrases = spec.Phrases;
            var sorts = spec.Sort;
            if (filter != null)
            {
                builder.AppendLine(string.Format(prompts.Filter, filter.ToUserFriendlyString()));
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
                builder.AppendLine(string.Format(prompts.Keywords, phraseBuilder.ToString()));
                builder.AppendLine();
            }
            if (sorts.Any())
            {
                var sortBuilder = new StringBuilder();
                var prefix = "";
                foreach (var sort in sorts)
                {
                    var dir = sort.Direction == SortDirection.Ascending ? prompts.Ascending : prompts.Descending;
                    sortBuilder.Append($"{prefix}{sort.Field} {dir}");
                    prefix = ", ";
                }
                builder.AppendLine(string.Format(prompts.Sort, sortBuilder.ToString()));
                builder.AppendLine();
            }
            if (spec.PageNumber > 0)
            {
                builder.AppendLine(string.Format(prompts.Page, spec.PageNumber));
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static string FirstResolution(this EntityRecommendation entity)
        {
            return (string) (entity.Resolution?["values"] as JArray)?.First();
        }

        public static string Resolution(this EntityRecommendation entity, string name)
        {
            return (string) (entity.Resolution?[name]);
        }

        // Entity2 is congruent or entirely inside entity1
        public static bool Contains(this EntityRecommendation entity1, EntityRecommendation entity2)
        {
            return entity1.StartIndex <= entity2.StartIndex
                && entity1.EndIndex >= entity2.EndIndex;
        }

        // Entity1 and entity2 share a range
        public static bool Overlaps(this EntityRecommendation entity1, EntityRecommendation entity2)
        {
            return (entity1.StartIndex >= entity2.StartIndex && entity1.StartIndex <= entity2.EndIndex)
                || (entity1.EndIndex >= entity2.StartIndex && entity1.EndIndex <= entity2.EndIndex);
        }

        // Entity1 and entity2 cover the exact same span
        public static bool Congruent(this EntityRecommendation entity1, EntityRecommendation entity2)
        {
            return entity1.StartIndex == entity2.StartIndex
                && entity1.EndIndex == entity2.EndIndex;
        }
    }
}
