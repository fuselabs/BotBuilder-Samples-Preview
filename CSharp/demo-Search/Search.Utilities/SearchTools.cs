using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Spatial;
using Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Azure
{
    public static partial class SearchTools
    {
        public static SearchSchema GetIndexSchema(string service, string adminKey, string indexName)
        {
            var schema = new SearchSchema();
            var adminClient = new SearchServiceClient(service, new SearchCredentials(adminKey));
            var fields = adminClient.Indexes.Get(indexName).Fields;
            foreach (var field in fields)
            {
                schema.AddField(ToSearchField(field));
            }
            return schema;
        }
        
        public static SearchField ToSearchField(Field field)
        {
            Type type;
            if (field.Type == DataType.Boolean) type = typeof(Boolean);
            else if (field.Type == DataType.DateTimeOffset) type = typeof(DateTime);
            else if (field.Type == DataType.Double) type = typeof(double);
            else if (field.Type == DataType.Int32) type = typeof(Int32);
            else if (field.Type == DataType.Int64) type = typeof(Int64);
            else if (field.Type == DataType.String) type = typeof(string);
            else if (field.Type == DataType.Collection(DataType.String)) type = typeof(string[]);
            else if (field.Type == DataType.GeographyPoint) type = typeof(GeographyPoint);
            else
            {
                throw new ArgumentException($"Cannot map {field.Type} to a C# type");
            }
            return new SearchField(field.Name, Microsoft.Bot.Builder.FormFlow.Advanced.Language.CamelCase(field.Name))
            {
                Type = type,
                IsFacetable = field.IsFacetable,
                IsFilterable = field.IsFilterable,
                IsKey = field.IsKey,
                IsRetrievable = field.IsRetrievable,
                IsSearchable = field.IsSearchable,
                IsSortable = field.IsSortable,
                FilterPreference = (field.IsFacetable ? PreferredFilter.Facet : PreferredFilter.None)
            };
        }
    }
}
