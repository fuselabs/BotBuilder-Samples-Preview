using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Search.Models;

namespace Search.Dialogs.UserInteraction
{
    public static class FacetDisplay
    {
        public static string Describe(GenericFacet facet, Dictionary<string, CanonicalValue> valueCanonicalizers)
        {
            string description;
            if (facet.Value is string)
            {
                description = (string)facet.Value;
                CanonicalValue value;
                if (valueCanonicalizers.TryGetValue(description, out value))
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

        public static List<Button> Buttons(PreferredFilter preference, IEnumerable<GenericFacet> choices, SearchField field, string desc)
        {
            var buttons = new List<Button>();
            if (preference == PreferredFilter.Facet)
            {
                foreach (var choice in choices)
                {
                    buttons.Add(
                        new Button(field.ValueSynonyms.Any() ? $"{choice.Value}" : $"{choice.Value} {desc}",
                            $"{choice.Value} ({choice.Count})"));
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
            return buttons;
        }
    }
}
