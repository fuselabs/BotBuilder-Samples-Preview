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

namespace Search.Tools.Extract
{
    internal class Program
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };

        private static int Apply(SearchIndexClient client, string valueField, string idField, string text,
            SearchParameters sp, Action<int, SearchResult> function,
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
                        function(total, result);
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

        private static int StreamApply(TextReader stream, Action<int, SearchResult> function, int samples)
        {
            string line;
            int count = 0;
            var schema = JsonConvert.DeserializeObject<SearchSchema>(stream.ReadLine());
            while ((line = stream.ReadLine()) != null && count < samples)
            {
                var result = new SearchResult();
                result.Document = new Document();
                var doc = JsonConvert.DeserializeObject<Document>(line);
                foreach(var entry in doc)
                {
                    if (entry.Value is JArray)
                    {
                        result.Document[entry.Key] = (from val in (entry.Value as JArray) select (string) val).ToArray<string>();
                    }
                    else
                    {
                        result.Document[entry.Key] = entry.Value;
                    }
                }
                function(count++, result);
            }
            return count;
        }

        private static void Process(int count,
            SearchResult result,
            IEnumerable<string> fields,
            Dictionary<string, Histogram<object>> histograms,
            TextWriter copyStream)
        {
            var doc = result.Document;
            if (copyStream != null)
            {
                var jsonDoc = JsonConvert.SerializeObject(doc);
                copyStream.WriteLine(jsonDoc);
            }
            foreach (var field in fields)
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
                "extract <serviceName> <indexName> <adminKey> [-a <file>] [-c <file>] [-f <facetList>] [-g <histogramPath>] [-h <histogramPath>] [-o <outputPath>]");
            Console.WriteLine("Generate <indexName>.json schema file.");
            Console.WriteLine(
                "-a <file> : Apply analysis to JSON file rather than search index.");
            Console.WriteLine(
                "-c <file> : Copy search index to local JSON file.");
            Console.WriteLine(
                "-f <facetList>: Comma seperated list of facet names for histogram.  By default all schema facets.");
            Console.WriteLine(
                "-g <histogramPath>: Generate a file with histogram information from index.  This can take a long time.");
            Console.WriteLine(
                "-h <histogramPath>: Use histogram to help generate schema.  This can be the just generated histogram.");
            Console.WriteLine("-o <schemaPath>: Where to put generated schema.");
            Console.WriteLine(
                "-s <samples>: Maximum number of rows to sample from index when doing -g.  All by default.");
            Console.WriteLine(
                "-u <uniqueThreshold>: Maximum number of unique string values for a field to be an attribute from -g.  By default is 100.  LUIS allows a total of 5000.");
            Console.WriteLine(
                "-v <field>: Field to order by when using -g.  There must be no more than 100,000 rows with the same value.  Will use key field if sortable and filterable.");
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

        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
            }
            var serviceName = args[0];
            var indexName = args[1];
            var adminKey = args[2];
            string[] facets = null;
            string generatePath = null;
            string histogramPath = null;
            string copyPath = null;
            string applyPath = null;
            var schemaPath = indexName + ".json";
            var samples = int.MaxValue;
            var uniqueValueThreshold = 100;
            string sortable = null;
            for (var i = 3; i < args.Length; ++i)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-a":
                        applyPath = NextArg(++i, args);
                        break;
                    case "-c":
                        copyPath = NextArg(++i, args);
                        break;
                    case "-f":
                        facets = NextArg(++i, args).Split(',').ToArray<string>();
                        break;
                    case "-g":
                        generatePath = NextArg(++i, args);
                        break;
                    case "-h":
                        histogramPath = NextArg(++i, args);
                        break;
                    case "-o":
                        schemaPath = NextArg(++i, args);
                        break;
                    case "-s":
                        samples = int.Parse(NextArg(++i, args));
                        break;
                    case "-u":
                        uniqueValueThreshold = int.Parse(NextArg(++i, args));
                        break;
                    case "-v":
                        sortable = NextArg(++i, args);
                        break;
                    default:
                        Usage($"{arg} is not understood.");
                        break;
                }
            }
            var schema = SearchTools.GetIndexSchema(serviceName, adminKey, indexName);
            if (generatePath != null)
            {
                if (sortable == null)
                {
                    foreach (var field in schema.Fields.Values)
                    {
                        if (field.IsKey && field.IsSortable && field.IsFilterable)
                        {
                            sortable = field.Name;
                        }
                    }
                    if (sortable == null)
                    {
                        Usage("You must specify a field with -v.");
                    }
                }
                var indexClient = new SearchIndexClient(serviceName, indexName, new SearchCredentials(adminKey));
                if (facets == null)
                {
                    facets = (from field in schema.Fields.Values
                              where (field.Type == typeof(string) || field.Type == typeof(string[])) && field.IsFilterable
                              select field.Name).ToArray();
                }
                var id = schema.Fields.Values.First((f) => f.IsKey);
                var histograms = new Dictionary<string, Histogram<object>>();
                var sp = new SearchParameters();
                var timer = Stopwatch.StartNew();
                var copyStream = copyPath == null ? null : new StreamWriter(new FileStream(copyPath, FileMode.Create));
                if (copyStream != null)
                {
                    copyStream.WriteLine(JsonConvert.SerializeObject(schema));
                }
                var applyStream = applyPath == null ? null : new StreamReader(new FileStream(applyPath, FileMode.Open));
                var results = applyStream == null
                    ? Apply(indexClient, sortable, id.Name, null, sp,
                    (count, result) => { Process(count, result, facets, histograms, copyStream); },
                    samples)
                : StreamApply(applyStream, (count, result) => Process(count, result, facets, histograms, null), samples);
                Console.WriteLine($"\nFound {results} in {timer.Elapsed.TotalSeconds}s");
                if (copyStream != null)
                {
                    copyStream.Dispose();
                }
                using (var stream = new FileStream(generatePath, FileMode.Create))
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
            if (histogramPath != null)
            {
                Dictionary<string, Histogram<object>> histograms;
                using (var stream = new FileStream(histogramPath, FileMode.Open))
                {
#if !NETSTANDARD1_6
                    var deserializer = new BinaryFormatter();
                    histograms = (Dictionary<string, Histogram<object>>) deserializer.Deserialize(stream);
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
                        if (counts.Counts().Count() < uniqueValueThreshold
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
            schema.Save(schemaPath);
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