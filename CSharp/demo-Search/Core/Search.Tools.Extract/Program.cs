using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using Search.Azure;
using Search.Models;
using Search.Utilities;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;

namespace Search.Tools.Extract
{
    internal class Program
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };

        private static async Task<int> Apply(SearchIndexClient client, string valueField, string idField, string text,
            SearchParameters sp, Func<int, SearchResult, Task> function,
            int max = int.MaxValue,
            int page = 1000)
        {
            var originalFilter = sp.Filter;
            var originalOrder = sp.OrderBy;
            var originalTop = sp.Top;
            var originalSkip = sp.Skip;
            var total = 0;
            object lastValue = null;
            object lastID = null;
            sp.OrderBy = new string[] { valueField };
            sp.Top = page;
            var results = client.Documents.Search(text, sp).Results;
            while (total < max && results.Any())
            {
                var skipping = lastValue != null;
                var newValue = false;
                var row = 0;
                var firstRowWithValue = 0;
                foreach (var result in results)
                {
                    var id = result.Document[idField];
                    if (skipping)
                    {
                        // Skip until we find the last processed id
                        skipping = !id.Equals(lastID);
                    }
                    else
                    {
                        var value = result.Document[valueField];
                        await function(total, result);
                        lastID = id;
                        if (!value.Equals(lastValue))
                        {
                            firstRowWithValue = row;
                            lastValue = value;
                            newValue = true;
                        }
                        if (++total == max)
                        {
                            break;
                        }
                    }
                    ++row;
                }
                if (skipping)
                {
                    throw new Exception($"Could not find id {lastID} in {lastValue}");
                }
                if (row == 1)
                {
                    // Last row in the table
                    break;
                }
                var toSkip = row - firstRowWithValue - 1;
                if (newValue)
                {
                    sp.Skip = toSkip;
                }
                else
                {
                    sp.Skip += toSkip;
                }
                sp.Filter = (originalFilter == null ? "" : $"({originalFilter}) and ") +
                            $"{valueField} ge {SearchTools.Constant(lastValue)}";
                results = client.Documents.Search(text, sp).Results;
            }
            sp.Filter = originalFilter;
            sp.OrderBy = originalOrder;
            sp.Top = originalTop;
            sp.Skip = originalSkip;
            return total;
        }

        private static async Task<int> StreamApply(TextReader stream, Func<int, SearchResult, Task> function, int samples)
        {
            string line;
            int count = 0;
            while ((line = stream.ReadLine()) != null && count < samples)
            {
                var result = new SearchResult();
                result.Document = new Document();
                var doc = JsonConvert.DeserializeObject<Document>(line);
                foreach (var entry in doc)
                {
                    if (entry.Value is JArray)
                    {
                        result.Document[entry.Key] = (from val in (entry.Value as JArray) select (string)val).ToArray<string>();
                    }
                    else
                    {
                        result.Document[entry.Key] = entry.Value;
                    }
                }
                await function(count++, result);
            }
            return count;
        }

        private static async Task Process(int count,
            SearchResult result,
            Parameters parameters,
            Dictionary<string, Histogram<object>> histograms,
            KeywordExtractor extractor,
            TextWriter copyStream)
        {
            var doc = result.Document;
            if (copyStream != null)
            {
                var jsonDoc = JsonConvert.SerializeObject(doc);
                copyStream.WriteLine(jsonDoc);
            }
            if (parameters.AnalyzeFields != null)
            {
                try
                {
                    foreach (var field in parameters.AnalyzeFields)
                    {
                        var value = doc[field];
                        if (value is string[])
                        {
                            foreach (var val in value as string[])
                            {
                                await extractor.AddTextAsync(val);
                            }
                        }
                        else
                        {
                            await extractor.AddTextAsync(value as string);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\nSuspended extracting analyze text keywords.\n{e.Message}");
                }
            }
            if (parameters.KeywordFields != null)
            {
                foreach(var field in parameters.KeywordFields)
                {
                    var value = doc[field];
                    if (value is string[])
                    {
                        foreach (var val in value as string[])
                        {
                            extractor.AddKeyword(val);
                        }
                    }
                    else
                    {
                        extractor.AddKeyword(value as string);
                    }
                }
            }
            foreach (var field in parameters.Facets)
            {
                var value = doc[field];
                if (value != null)
                {
                    Histogram<object> histogram;
                    if (!histograms.TryGetValue(field, out histogram))
                    {
                        histogram = histograms[field] = new Histogram<object>();
                    }
                    if (value is string[])
                    {
                        foreach (var val in value as string[])
                        {
                            histogram.Add(val);
                        }
                    }
                    else
                    {
                        histogram.Add(value);
                    }
                }
            }
            if (count % 100 == 0)
            {
                Console.Write($"\n{count}: ");
            }
            else
            {
                Console.Write(".");
            }
        }

        private static void Usage(string msg = null)
        {
            Console.WriteLine(
                "extract <Service name> <Index name> <Admin key> [-ad <domain>] [-af <fieldList>] [-ak <key>] [-al <language>] [-c <file>] [-f <facetList>] [-g <path>] [-h <path>] [-j <jsonFile>] [-kf <fieldList>] [-km <max>] [-kt <threshold>] [-o <outputPath>] [-u <threshold>] [-v <field>]");
            Console.WriteLine(
                @"Generate <parameters.IndexName>.json schema file.
If you generate a histogram using -g and -h, it will be used to determine attributes if less than -u unique values are found.
You can find keywords either through -kf for actual keywords or -af to generate keywords using the text analytics cognitive service.");
            Console.WriteLine(
                "-ad <domain>: Analyze text domain, westus.api.cognitive.microsoft.com by default.");
            Console.WriteLine(
                "-af <fieldList> : Comma seperated fields to analyze for keywords.");
            Console.WriteLine(
                "-ak <key> : Key for calling analyze text cognitive service.");
            Console.WriteLine(
                "-al <language> : Language to use for keyword analysis, en by default.");
            Console.WriteLine(
                "-c <file> : Copy search index to local JSON file that can be used via -j instead of talking to Azure Search service.");
            Console.WriteLine(
                "-f <facetList>: Comma seperated list of facet names for histogram.  By default all schema parameters.Facets.");
            Console.WriteLine(
                "-g <path>: Generate a file with histogram information from index.  This can take a long time.");
            Console.WriteLine(
                "-h <path>: Use histogram to help generate schema.  This can be the just generated histogram.");
            Console.WriteLine(
                "-j <file> : Apply analysis to JSON file rather than search index.");
            Console.WriteLine(
                "-kf <fieldList> : Comma seperated fields that contain keywords.");
            Console.WriteLine(
                "-km <max> : Maximum number of keywords to extract, default is 10,000.");
            Console.WriteLine(
                "-kt <threshold> : Minimum number of docs required to be a keyword, default is 5.");
            Console.WriteLine(
                "-o <path>: Where to put generated schema.");
            Console.WriteLine(
                "-s <samples>: Maximum number of rows to sample from index when doing -g.  All by default.");
            Console.WriteLine(
                "-u <threshold>: Maximum number of unique string values for a field to be an attribute from -g.  By default is 100.  LUIS allows a total of 5000.");
            Console.WriteLine(
                "-v <field>: Field to order by when using -g.  There must be no more than 100,000 rows with the same value.  Will use key field if sortable and filterable.");
            Console.WriteLine(
                "{} can be used to comment out arguments.");
            Environment.Exit(-1);
        }

        private static string NextArg(int i, string[] args)
        {
            string arg = null;
            if (i < args.Length)
            {
                arg = args[i];
            }
            else
            {
                Usage();
            }
            return arg;
        }

        private class Parameters
        {
            public string AdminKey;
            public string AnalyzeKey;
            public string AnalyzeDomain = "westus.api.cognitive.microsoft.com";
            public string[] AnalyzeFields = null;
            public string AnalyzeLanguage = "en";
            public string ApplyPath;
            public string CopyPath;
            public string[] Facets;
            public string GeneratePath;
            public string HistogramPath;
            public string IndexName;
            public string[] KeywordFields;
            public int KeywordMax = 10000;
            public int KeywordThreshold = 5;
            public int Samples = int.MaxValue;
            public string SchemaPath;
            public string ServiceName;
            public string Sortable;
            public int UniqueValueThreshold = 100;

            public void Display(TextWriter writer)
            {
                foreach(var field in typeof(Parameters).GetFields())
                {
                    var value = field.GetValue(this);
                    if (value != null)
                    {
                        writer.Write($"{field.Name}:");
                        if (value is IEnumerable && !(value is string))
                        {
                            foreach(var val in value as IEnumerable)
                            {
                                writer.Write($" {val}");
                            }
                            writer.WriteLine();
                        }
                        else
                        {
                            writer.WriteLine($" {value}");
                        }
                    }
                }
            }
        };

        private static async Task MainAsync(Parameters parameters)
        {
            if (parameters.AnalyzeFields == null ? parameters.AnalyzeKey != null : parameters.AnalyzeKey == null)
            {
                Console.WriteLine("In order to analyze keywords you need both -ak and -af parameters.");
            }
            var applyStream = parameters.ApplyPath == null ? null : new StreamReader(new FileStream(parameters.ApplyPath, FileMode.Open));
            SearchSchema schema;
            if (applyStream == null)
            {
                schema = SearchTools.GetIndexSchema(parameters.ServiceName, parameters.AdminKey, parameters.IndexName);
            }
            else
            {
                schema = JsonConvert.DeserializeObject<SearchSchema>(applyStream.ReadLine());
                applyStream.ReadLine();
            }
            if (parameters.GeneratePath != null)
            {
                if (parameters.Sortable == null)
                {
                    foreach (var field in schema.Fields.Values)
                    {
                        if (field.IsKey && field.IsSortable && field.IsFilterable)
                        {
                            parameters.Sortable = field.Name;
                        }
                    }
                    if (parameters.Sortable == null)
                    {
                        Usage("You must specify a field with -v.");
                    }
                }
                var indexClient = new SearchIndexClient(parameters.ServiceName, parameters.IndexName, new SearchCredentials(parameters.AdminKey));
                if (parameters.Facets == null)
                {
                    parameters.Facets = (from field in schema.Fields.Values
                                         where (field.Type == typeof(string) || field.Type == typeof(string[])) && field.IsFilterable
                                         select field.Name).ToArray();
                }
                var id = schema.Fields.Values.First((f) => f.IsKey);
                var histograms = new Dictionary<string, Histogram<object>>();
                var sp = new SearchParameters();
                var timer = Stopwatch.StartNew();
                var copyStream = parameters.CopyPath == null ? null : new StreamWriter(new FileStream(parameters.CopyPath, FileMode.Create));
                if (copyStream != null)
                {
                    copyStream.WriteLine(JsonConvert.SerializeObject(schema));
                }
                var extractor = parameters.AnalyzeKey != null ? new KeywordExtractor(parameters.AnalyzeKey, parameters.AnalyzeLanguage, parameters.AnalyzeDomain) : null;
                var results = await (applyStream == null
                    ? Apply(indexClient, parameters.Sortable, id.Name, null, sp,
                    (count, result) => Process(count, result, parameters, histograms, extractor, copyStream),
                    parameters.Samples)
                : StreamApply(applyStream, (count, result) => Process(count, result, parameters, histograms, extractor, null), parameters.Samples));
                Console.WriteLine($"\nFound {results} in {timer.Elapsed.TotalSeconds}s");
                if (copyStream != null)
                {
                    copyStream.Dispose();
                }
                if (parameters.AnalyzeFields != null || parameters.KeywordFields != null)
                {
                    var counts = await extractor.KeywordsAsync();
                    var topN = (from count in counts where count.Value >= parameters.KeywordThreshold orderby count.Value descending select count.Key).Take(parameters.KeywordMax);
                    var sorted = (from keyword in topN orderby keyword ascending select keyword);
                    schema.Keywords = string.Join(",", sorted.ToArray());
                }
                using (var stream = new FileStream(parameters.GeneratePath, FileMode.Create))
                {
#if !NETSTANDARD1_6
                    var serializer = new BinaryFormatter();
                    serializer.Serialize(stream, histograms);
#else
                    var jsonHistograms = JsonConvert.SerializeObject(histograms);
                    stream.Write(Encoding.UTF8.GetBytes(jsonHistograms), 0, Encoding.UTF8.GetByteCount(jsonHistograms));
#endif
                }
            }
            if (parameters.HistogramPath != null)
            {
                Dictionary<string, Histogram<object>> histograms;
                using (var stream = new FileStream(parameters.HistogramPath, FileMode.Open))
                {
#if !NETSTANDARD1_6
                    var deserializer = new BinaryFormatter();
                    histograms = (Dictionary<string, Histogram<object>>)deserializer.Deserialize(stream);
#else
                    using (TextReader reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        histograms = JsonConvert.DeserializeObject<Dictionary<string, Histogram<object>>>(text, jsonSettings);
                    }
#endif

                    foreach (var histogram in histograms)
                    {
                        var field = schema.Field(histogram.Key);
                        var counts = histogram.Value;
                        if (counts.Counts().Count() < parameters.UniqueValueThreshold
                            && counts.Values().FirstOrDefault() != null && counts.Values().First() is string)
                        {
                            var vals = new List<Synonyms>();
                            foreach (var value in counts.Pairs())
                            {
                                var canonical = value.Key as string;
                                if (!string.IsNullOrWhiteSpace(canonical))
                                {
                                    // Remove punctuation and trimming
                                    var alt = Normalize(canonical);
                                    var synonyms = new Synonyms(canonical, alt);
                                    vals.Add(synonyms);
                                }
                            }
                            field.ValueSynonyms = vals.ToArray();
                        }
                    }
                }
            }
            schema.Save(parameters.SchemaPath);
        }

        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
            }
            var parameters = new Parameters();
            parameters.ServiceName = args[0];
            parameters.IndexName = args[1];
            parameters.AdminKey = args[2];
            parameters.SchemaPath = parameters.IndexName + ".json";
            for (var i = 3; i < args.Length; ++i)
            {
                var arg = args[i];
                if (arg.StartsWith("{"))
                {
                    while (!args[i].EndsWith("}") && ++i < args.Count())
                    {
                    }
                    if (i == args.Count())
                    {
                        break;
                    }
                }
                else
                {
                    switch (arg)
                    {
                        case "-ad":
                            parameters.AnalyzeDomain = NextArg(++i, args);
                            break;
                        case "-af":
                            parameters.AnalyzeFields = NextArg(++i, args).Split(',');
                            break;
                        case "-ak":
                            parameters.AnalyzeKey = NextArg(++i, args);
                            break;
                        case "-al":
                            parameters.AnalyzeLanguage = NextArg(++i, args);
                            break;
                        case "-am":
                            parameters.KeywordMax = int.Parse(NextArg(++i, args));
                            break;
                        case "-at":
                            parameters.KeywordThreshold = int.Parse(NextArg(++i, args));
                            break;
                        case "-c":
                            parameters.CopyPath = NextArg(++i, args);
                            break;
                        case "-f":
                            parameters.Facets = NextArg(++i, args).Split(',');
                            break;
                        case "-g":
                            parameters.GeneratePath = NextArg(++i, args);
                            break;
                        case "-h":
                            parameters.HistogramPath = NextArg(++i, args);
                            break;
                        case "-j":
                            parameters.ApplyPath = NextArg(++i, args);
                            break;
                        case "-k":
                            parameters.KeywordFields = NextArg(++i, args).Split(',');
                            break;
                        case "-o":
                            parameters.SchemaPath = NextArg(++i, args);
                            break;
                        case "-s":
                            parameters.Samples = int.Parse(NextArg(++i, args));
                            break;
                        case "-u":
                            parameters.UniqueValueThreshold = int.Parse(NextArg(++i, args));
                            break;
                        case "-v":
                            parameters.Sortable = NextArg(++i, args);
                            break;
                        default:
                            Usage($"{arg} is not understood.");
                            break;
                    }
                }
            }
            parameters.Display(Console.Out);
            MainAsync(parameters).Wait();
        }

        public static string Normalize(string input)
        {
            var start = 0;
            for (; start < input.Length; ++start)
            {
                if (!char.IsPunctuation(input[start]))
                {
                    break;
                }
            }
            var end = input.Length;
            for (; end > 0; --end)
            {
                if (!char.IsPunctuation(input[end - 1]))
                {
                    break;
                }
            }
            return end > start ? input.Substring(start, end - start) : "";
        }
    }
}