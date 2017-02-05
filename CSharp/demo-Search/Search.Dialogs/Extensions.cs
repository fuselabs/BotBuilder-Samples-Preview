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
        public static string Description(this SearchQueryBuilder query)
        {
            var builder = new StringBuilder();
            var filter = query.Spec.Filter;
            var phrases = query.Spec.Phrases;
            var sorts = query.Spec.Sort;
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
            if (query.PageNumber > 0)
            {
                builder.AppendLine(string.Format(Prompts.Page, query.PageNumber));
                builder.AppendLine();
            }
            return builder.ToString();
        }
    }
}
