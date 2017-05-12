using Microsoft.Bot.Builder.Dialogs;
using Search.Azure;
using Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Search.Dialogs.UserInteraction
{
    public enum ResourceType
    {
        AddedToListPrompt,          // 0-ItemID
        AddKeywordPrompt,
        AddOrRemoveKeywordPrompt,
        FacetPrompt,
        FacetValuePrompt,           // 0-ItemID
        InitialPrompt,
        ListPrompt,
        NoResultsPrompt,            // 0-Filter
        NotAddedPrompt,
        NotUnderstoodPrompt,
        RefinePrompt,
        RemovedFromListPrompt,      // 0-ItemID
        UnknownItemPrompt,
        // Status messages
        Ascending,
        Count,                      // 0-Count
        Descending,
        Filter,                     // 0-Filter
        Keywords,                   // 0-Keywords
        Page,                       // 0-Page#
        Selected,                   // 0-Number selected
        Sort                        // 0-Sort
    };

    public enum ButtonType
    {
        // Buttons
        Add,                // 0-ItemID
        Any,                // 0-Field
        AnyNumber,          // 0-Field
        Finished,
        Keyword,
        List,
        NextPage,
        Quit,
        Refine,
        Remove,             // 0-ItemID
        RemoveKeyword,      // 0-Keyword
        StartOver
    };

    public enum FieldType
    {
        // 0-Field, 1-Typical, 2-Min, 3-Max, 4-Most common, 5-Example Min, 6-Example Max
        IntroHint,
        FilterPrompt,
        NoValuesPrompt
    };

    public interface IResource
    {
        Button ButtonResource(ButtonType type, params object[] parameters);
        string Resource(ResourceType type, params object[] parameters);
        string FieldResource(FieldType type, string field, SearchSchema schema, IDialogContext context);
    }

    [Serializable]
    public class ResourceGenerator : IResource
    {
        private readonly Prompts _prompts;
        private readonly Random _random = new Random();

        public ResourceGenerator(Prompts prompts)
        {
            _prompts = prompts;
        }

        public Button ButtonResource(ButtonType type, params object[] parameters)
        {
            var button = (Button)_prompts.GetType().GetField(type.ToString()).GetValue(_prompts);
            if (parameters.Length > 0)
            {
                button = new Button(string.Format(button.Label, parameters),
                    button.Message != null ? string.Format(button.Message, parameters) : null);
            }
            return button;
        }

        private void Examples(SearchField field, out double typical, out double min, out double max)
        {
            var examples = field.Examples;
            typical = double.Parse(examples.First());
            min = double.MaxValue;
            max = double.MinValue;
            foreach (var example in examples.Skip(1))
            {
                double val;
                if (double.TryParse(example, out val))
                {
                    if (val < min) min = val;
                    if (val > max) max = val;
                }
            }
        }

        private string FieldHint(SearchField field)
        {
            string hint = null;
            if (field.ValueSynonyms.Any())
            {
                var prompt = _prompts.ValueHints[_random.Next(_prompts.ValueHints.Length)];
                var builder = new StringBuilder();
                foreach(var synonym in field.ValueSynonyms)
                {
                    if (field.Examples.Contains(synonym.Canonical))
                    {
                        builder.Append(" \"" + synonym.Alternatives[_random.Next(synonym.Alternatives.Length)] + "\"");
                    }
                }
                hint = string.Format(prompt, field.Description(), builder.ToString());
            }
            else if (field.IsMoney)
            {
                double typical, min, max;
                Examples(field, out typical, out min, out max);
                var prompt = _prompts.MoneyHints[_random.Next(_prompts.MoneyHints.Length)];
                hint = string.Format(prompt, field.Description(), typical, min, max);
            }
            else if (field.Type.IsNumeric())
            {
                double typical, min, max;
                Examples(field, out typical, out min, out max);
                var prompt = _prompts.NumberHints[_random.Next(_prompts.NumberHints.Length)];
                hint = string.Format(prompt, field.Description(), typical, min, max);
            }
            return hint;
        }

        public string FieldResource(FieldType type, string fieldName, SearchSchema schema, IDialogContext context)
        {
            string prompt = null;
            if (type == FieldType.IntroHint)
            {
                // 1) Randomly select field of each type
                var numbers = (from field in schema.Fields.Values where field.Type.IsNumeric() && !field.IsMoney select field).ToArray();
                var values = (from field in schema.Fields.Values where field.ValueSynonyms.Any() select field).ToArray();
                var money = (from field in schema.Fields.Values where field.IsMoney select field).ToArray();
                var builder = new StringBuilder();
                if (numbers.Any())
                {
                    var field = numbers[_random.Next(numbers.Length)];
                    builder.Append("* ");
                    builder.AppendLine(FieldHint(field));
                    builder.AppendLine();
                }
                if (values.Any())
                {
                    var field = values[_random.Next(values.Length)];
                    builder.Append("* ");
                    builder.AppendLine(FieldHint(field));
                    builder.AppendLine();
                }
                if (money.Any())
                {
                    var field = money[_random.Next(money.Length)];
                    builder.Append("* ");
                    builder.AppendLine(FieldHint(field));
                    builder.AppendLine();
                }
                if (builder.Length > 0)
                {
                    prompt = string.Format(_prompts.IntroHint, Environment.NewLine + Environment.NewLine + builder.ToString());
                }
            }
            else
            {
                var field = schema.Field(fieldName);
                if (field.Type.IsNumeric())
                {
                    double typical, min, max;
                    prompt = (string)_prompts.GetType().GetField(type.ToString() + "Number").GetValue(_prompts);
                    Examples(field, out typical, out min, out max);
                    prompt = string.Format(prompt, field.Description(), field.Min, field.Max, typical, min, max);
                }
                else
                {
                    var typical = field.Examples.FirstOrDefault();
                    prompt = (string)_prompts.GetType().GetField(type.ToString() + "String").GetValue(_prompts);
                    prompt = string.Format(prompt, field.Description(), typical);
                }
            }
            return prompt;
        }

        public string Resource(ResourceType type, params object[] parameters)
        {
            var prompt = (string)_prompts.GetType().GetField(type.ToString()).GetValue(_prompts);
            if (parameters.Length > 0)
            {
                prompt = string.Format(prompt, parameters);
            }
            return prompt;
        }
    }

    [Serializable]
    public class Prompts
    {
        // Prompts
        public string AddedToListPrompt = "{0} was added to your list.";
        public string AddKeywordPrompt = "Type a keyword phrase to search for.";
        public string AddOrRemoveKeywordPrompt = "Type a keyword phrase to search for, or select what you would like to remove.";
        public string FacetPrompt = "What would you like to refine by?";
        public string FacetValuePrompt = "What value for {0} would you like to filter by?";
        public string FilterPromptNumber = "{0} can have values between {2} and {3}.  Enter a filter like \"between {4} and {5}\".";
        public string FilterPrompString = "Enter a value for {0} like \"{1}\".";
        public string InitialPrompt = "Please describe in your own words what you would like to find?";
        public string ListPrompt = "Here is what you have selected so far.";
        public string NoResultsPrompt = "{0}Found no results so I undid your last change.  You can refine again.";
        public string NotAddedPrompt = "You have not added anything yet.";
        public string NotUnderstoodPrompt = "I did not understand what you said.";
        public string NoValuesPrompt = "There are no values to filter by for {0}.";
        public string RefinePrompt = "Refine your search or select an operation.";
        public string RemovedFromListPrompt = "{0} was removed from your list.";
        public string UnknownItemPrompt = "That is not an item in the current results.";

        // Buttons
        public Button Add = new Button("Add to List", "ADD:{0}");
        public Button Any = new Button("Any", "Any {0}");
        public Button AnyNumber = new Button("Any", "Any number of {0}");
        public Button Finished = new Button("Finished");
        public Button Keyword = new Button("Keyword");
        public Button List = new Button("List");
        public Button NextPage = new Button("Next Page");
        public Button Quit = new Button("Quit");
        public Button Refine = new Button("Refine");
        public Button Remove = new Button("Remove from List", "REMOVE:{0}");
        public Button RemoveKeyword = new Button("{0}", "remove {0}");
        public Button StartOver = new Button("Start Over");

        // Status
        public string Ascending = "Ascending";
        public string Count = "**Total results**: {0}";
        public string Descending = "Descending";
        public string Filter = "**Filter**: {0}";
        public string Keywords = "**Keywords**: {0}";
        public string Page = "**Page**: {0}";
        public string Selected = "**Kept so far**: {0}";
        public string Sort = "**Sort**: {0}";

        // Hints
        public string IntroHint = "Some of the things I understand include: {0}";
        public string[] NumberHints = new string[]
        {
            // 0-Field, 1-Typical, 2-MinExample, 3-MaxExample
            "at least {1} {0}",
            "no more than {1} {0}",
            "between {2} and {3} {0}",
            "{1}+ {0}",
            "{2}-{3} {0}"
        };

        public string[] MoneyHints = new string[]
        {
            // 0-Field, 1-Typical, 2-MinExample, 3-MaxExample
            "{0} is less than ${1}",
            "{0} is more than ${1}",
            "more than ${1}",
            "less than ${1}",
            "at least ${1}",
            "between ${2} and ${3}",
            "${2}-${3}"
        };
        public string[] ValueHints = new string[]
        {
            // 0-Field, 1-list of possible values
            "{1} for {0}"
        };
    }
}