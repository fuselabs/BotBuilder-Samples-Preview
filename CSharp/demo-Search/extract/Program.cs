﻿namespace Search.Extract
{
    using Microsoft.Azure.Search;
    using Microsoft.Azure.Search.Models;
    using Search.Utilities;
    using System;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Search.Models;

    class Program
    {
        static int Apply(SearchIndexClient client, string valueField, string idField, string text, SearchParameters sp, Action<int, SearchResult> function,
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
                bool skipping = lastValue != null;
                bool newValue = false;
                int row = 0;
                int firstRowWithValue = 0;
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
                sp.Filter = (originalFilter == null ? "" : $"({originalFilter}) and ") + $"{valueField} ge {lastValue}";
                results = client.Documents.Search(text, sp).Results;
            }
            sp.Filter = originalFilter;
            sp.OrderBy = originalOrder;
            sp.Top = originalTop;
            sp.Skip = originalSkip;
            return total;
        }

        static void Process(int count,
            SearchResult result,
            IEnumerable<string> fields, Dictionary<string, Histogram<object>> histograms)
        {
            var doc = result.Document;
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
                    histogram.Add(value);
                }
            }
            if ((count % 100) == 0)
            {
                Console.Write($"\n{count}: ");
            }
            else
            {
                Console.Write(".");
            }
        }

        static void Usage(string msg = null)
        {
            Console.WriteLine("extract <serviceName> <indexName> <adminKey> [-f <facetList>] [-g <histogramPath>] [-h <histogramPath>] [-o <outputPath>]");
            Console.WriteLine("Generate <indexName>.json schema file.");
            Console.WriteLine("-f <facetList>: Comma seperated list of facet names for histogram.  By default all schema facets.");
            Console.WriteLine("-g <histogramPath>: Generate an <indexName>-histogram.bin file with histogram information from index.");
            Console.WriteLine("-h <histogramPath>: Use histogram to help generate schema.");
            Console.WriteLine("-o <outputFile>: Where to put generated schema and histogram files.");
            Environment.Exit(-1);
        }

        static string NextArg(int i, string[] args)
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

        static void Main(string[] args)
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
            string schemaPath = indexName + ".json";
            for (var i = 3; i < args.Length; ++i)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-f": facets = NextArg(++i, args).Split(',').ToArray<string>(); break;
                    case "-g": generatePath = NextArg(++i, args); break;
                    case "-h": histogramPath = NextArg(++i, args); break;
                    case "-o": schemaPath = NextArg(++i, args); break;
                    default: Usage($"{arg} is not understood."); break;
                }
            }
            var schema = Search.Azure.SearchTools.GetIndexSchema(serviceName, adminKey, indexName);
            if (generatePath != null)
            {
                var indexClient = new SearchIndexClient(serviceName, indexName, new SearchCredentials(adminKey));
                if (facets == null)
                {
                    facets = (from field in schema.Fields.Values where field.IsFacetable select field.Name).ToArray();
                }
                var sortable = schema.Fields.Values.First((f) => f.IsSortable);
                var id = schema.Fields.Values.First((f) => f.IsKey);
                var path = ".";
                var histograms = new Dictionary<string, Histogram<object>>();
                var sp = new SearchParameters();
                var timer = Stopwatch.StartNew();
                var results = Apply(indexClient, sortable.Name, id.Name, null, sp,
                    (count, result) =>
                    {
                        Process(count, result, facets, histograms);
                    }
                    // , 1000
                    );
                Console.WriteLine($"\nFound {results} in {timer.Elapsed.TotalSeconds}s");
                using (var stream = new FileStream(Path.Combine(path, "-histograms.bin"), FileMode.Create))
                {
                    var serializer = new BinaryFormatter();
                    serializer.Serialize(stream, histograms);
                }
            }
            if (histogramPath != null)
            {
                // TODO: Use histogram to infer some schema information
            }
            schema.Save(schemaPath);
        }
    }
}
