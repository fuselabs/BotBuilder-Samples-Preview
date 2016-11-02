using Search.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Search.Models;
using Newtonsoft.Json;

namespace Search.Generate
{
    class Program
    {
        const string propertyName = "propertyname";

        static void Usage()
        {
            Console.WriteLine("generate <schemaFile> <templateFile> [-h <histogramFile>] [-l <luis subscription>]");
            Console.WriteLine("Generate a <schemaFile>Model.Json file from <schemaFile> and <templateFile> which is an exported LUIS model.");
            Console.WriteLine("-l <LUIS subscription> : Will upload both SearchTemplate.json and <schemaFile>Model.json as LUIS models.");
            System.Environment.Exit(-1);
        }

        static void Clear(dynamic model)
        {
            foreach (var feature in model.model_features)
            {
                if (feature.name == "Properties")
                {
                    feature.words = "";
                }
            }
        }

        static string Normalize(string word)
        {
            return word.Trim().ToLower();
        }

        static string AddSynonyms(string original, Synonyms synonyms)
        {
            var builder = new StringBuilder(original);
            var prefix = string.IsNullOrEmpty(original) ? "" : ",";
            foreach (var alt in synonyms.Alternatives)
            {
                builder.Append(prefix);
                builder.Append(Normalize(alt));
                prefix = ",";
            }
            return builder.ToString();
        }

        static string AddValueSynonyms(string original, IEnumerable<Synonyms> synonyms)
        {
            foreach (var synonym in synonyms)
            {
                original = AddSynonyms(original, synonym);
            }
            return original;
        }

        static void AddDescription(dynamic model, string appName, string[] args)
        {
            model.desc = $"LUIS model generated via the command generate {string.Join(" ", args)}";
            model.name = appName;
        }

        static dynamic Feature(dynamic model, string name)
        {
            dynamic match = null;
            foreach (var feature in model.model_features)
            {
                if (feature.name == "Properties")
                {
                    match = feature;
                    break;
                }
            }
            return match;
        }

        static void AddComparison(dynamic model, SearchField field)
        {
            var feature = Feature(model, "Properties");
            feature.words = AddSynonyms((string)feature.words, field.NameSynonyms);
        }

        static void AddAttribute(dynamic model, SearchField field)
        {
            dynamic newFeature = new JObject();
            newFeature.name = field.Name;
            newFeature.mode = true;
            newFeature.activated = true;
            newFeature.words = AddValueSynonyms("", field.ValueSynonyms);
            model.model_features.Add(newFeature);

            dynamic newEntity = new JObject();
            newEntity.name = field.Name;
            model.entities.Add(newEntity);

            dynamic newUtterance = new JObject();
            var text = field.ValueSynonyms[0].Alternatives[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            newUtterance.text = string.Join(" ", text);
            newUtterance.intent = "Filter";
            var entities = new JArray();
            dynamic attribute = new JObject();
            attribute.entity = field.Name;
            attribute.startPos = 0;
            attribute.endPos = text.Count() - 1;
            entities.Add(attribute);
            newUtterance.entities = entities;
            model.utterances.Add(newUtterance);
        }

        static void ReplacePropertyNames(dynamic model)
        {
            var rand = new Random();
            var feature = Feature(model, "Properties");
            var words = ((string)feature.words).Split(',');
            foreach (var utterance in model.utterances)
            {
                var text = (string)utterance.text;
                var tokens = text.Split(' ').ToList();
                var properties = (from prop in (IEnumerable<dynamic>)utterance.entities where prop.entity == "Property" select prop).ToList();
                for (var i = 0; i < tokens.Count();)
                {
                    var token = tokens[i];
                    if (token == propertyName)
                    {
                        var word = words[rand.Next(words.Length)];
                        var wordTokens = word.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        tokens.RemoveAt(i);
                        tokens.InsertRange(i, wordTokens);
                        i += wordTokens.Length;
                        var offset = wordTokens.Count() - 1;
                        if (offset > 0)
                        {
                            var property = properties.First();
                            properties.RemoveAt(0);
                            foreach (var entity in utterance.entities)
                            {
                                if (entity.startPos > property.startPos)
                                {
                                    entity.startPos += offset;
                                }
                                if (entity.endPos >= property.startPos)
                                {
                                    entity.endPos += offset;
                                }
                            }
                        }
                    }
                    else
                    {
                        ++i;
                    }
                }
                utterance.text = string.Join(" ", tokens);
            }
        }

        static void Main(string[] args)
        {
            if (args.Count() < 2)
            {
                Usage();
            }
            string schemaPath = args[0];
            string templatePath = args[1];
            string modelPath = Path.Combine(Path.GetDirectoryName(schemaPath), Path.GetFileNameWithoutExtension(schemaPath) + "Model.json");
            string modelName = Path.GetFileNameWithoutExtension(schemaPath) + "Model";
            string histogramPath = null;
            string LUISKey = null;
            for (var i = 2; i < args.Count(); ++i)
            {
                var arg = args[i];
                switch (arg.Trim().ToLower())
                {
                    case "-h":
                        if (++i < args.Length)
                        {
                            histogramPath = args[i];
                        }
                        else
                        {
                            Usage();
                        }
                        break;
                    case "-l":
                        if (++i < args.Length)
                        {
                            LUISKey = args[i];
                        }
                        else
                        {
                            Usage();
                        }
                        break;
                }
            }

            /*
            // TODO: Remove this--only for testing
            {
                var rsschema = SearchSchema.Load(@"C:\Users\chrimc\Source\Repos\BotBuilder-Samples-Preview\CSharp\demo-Search\RealEstateBot\Dialogs\RealEstate.json");
                var dialog = new Search.Dialogs.SearchLanguageDialog(rsschema, "", "");
                var luis = JsonConvert.DeserializeObject<Microsoft.Bot.Builder.Luis.Models.LuisResult>(File.ReadAllText(@"c:\tmp\props.json"));
                dialog.ProcessComparison(null, luis);
                System.Environment.Exit(-1);
            }
            */

            var schema = SearchSchema.Load(File.ReadAllText(schemaPath));
            var template = JObject.Parse(File.ReadAllText(templatePath));
            Clear(template);
            AddDescription(template, modelName, args);
            foreach (var field in schema.Fields.Values)
            {
                if (field.Type == typeof(Int32)
                    || field.Type == typeof(Int64)
                    || field.Type == typeof(double))
                {
                    AddComparison(template, field);
                }
                else if (field.Type == typeof(string)
                    && field.ValueSynonyms.Length > 0)
                {
                    AddAttribute(template, field);
                }
            }
            ReplacePropertyNames(template);
            using (var stream = new StreamWriter(modelPath))
            {
                stream.Write(JsonConvert.SerializeObject(template, Formatting.Indented));
            }
            if (LUISKey != null)
            {
                Console.WriteLine("Uploading LUIS models");
                Task.WaitAll(
                    LUISTools.CreateModelAsync(LUISKey, "SearchTemplate", templatePath),
                    LUISTools.CreateModelAsync(LUISKey, modelName, modelPath));
            }
            /*
            Dictionary<string, Histogram<object>> histograms;
            using (var stream = new FileStream(histogramPath, FileMode.Open))
            {
                var serializer = new BinaryFormatter();
                histograms = (Dictionary<string, Histogram<object>>)serializer.Deserialize(stream);
            }
            foreach (var histogram in histograms)
            {
            }
            */
        }
    }
}
