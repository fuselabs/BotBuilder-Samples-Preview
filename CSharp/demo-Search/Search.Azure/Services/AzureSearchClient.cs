namespace Search.Azure.Services
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Search;
    using Microsoft.Azure.Search.Models;
    using Search.Models;
    using Search.Services;
    using Microsoft.Spatial;

    public class AzureSearchClient : ISearchClient
    {
        private readonly ISearchIndexClient searchClient;
        private readonly IMapper<DocumentSearchResult, GenericSearchResult> mapper;
        private SearchSchema schema = new SearchSchema();

        public AzureSearchClient(IMapper<DocumentSearchResult, GenericSearchResult> mapper)
        {
            this.mapper = mapper;
            var serviceName = ConfigurationManager.AppSettings["SearchDialogsServiceName"];
            var indexName = ConfigurationManager.AppSettings["SearchDialogsIndexName"];
            var serviceKey = ConfigurationManager.AppSettings["SearchDialogsServiceKey"];
            var adminKey = ConfigurationManager.AppSettings["SearchDialogsServiceAdminKey"];
            if (adminKey != null)
            {
                schema = SearchTools.GetIndexSchema(serviceName, adminKey, indexName);
            }
            var client = new SearchServiceClient(serviceName, new SearchCredentials(serviceKey));
            searchClient = client.Indexes.GetClient(indexName);
        }

        public async Task<GenericSearchResult> SearchAsync(SearchQueryBuilder queryBuilder, string refiner)
        {
            var documentSearchResult = await this.searchClient.Documents.SearchAsync(queryBuilder.SearchText, BuildParameters(queryBuilder, refiner));

            return this.mapper.Map(documentSearchResult);
        }

        public void AddFields(IEnumerable<Field> fields)
        {
            foreach (var field in fields)
            {
                schema.AddField(SearchTools.ToSearchField(field));
            }
        }

        private static string ToFilter(SearchField field, FilterExpression expression)
        {
            string filter = "";
            if (expression.Values.Length > 0)
            {
                var constant = Constant(expression.Values[0]);
                string op = null;
                bool connective = false;
                switch (expression.Operator)
                {
                    case Operator.LessThan: op = "lt"; break;
                    case Operator.LessThanOrEqual: op = "le"; break;
                    case Operator.Equal: op = "eq"; break;
                    case Operator.GreaterThan: op = "gt"; break;
                    case Operator.GreaterThanOrEqual: op = "ge"; break;
                    case Operator.Or: op = "or"; connective = true; break;
                    case Operator.And: op = "and"; connective = true; break;
                }
                if (connective)
                {
                    var builder = new StringBuilder();
                    var seperator = string.Empty;
                    builder.Append('(');
                    foreach (var child in expression.Values)
                    {
                        builder.Append(seperator);
                        builder.Append(ToFilter(field, (FilterExpression)child));
                        seperator = $" {op} ";
                    }
                    builder.Append(')');
                    filter = builder.ToString();
                }
                if (field.Type == typeof(string[]))
                {
                    if (expression.Operator != Operator.Equal)
                    {
                        throw new NotSupportedException();
                    }
                    filter = $"{field.Name}/any(z: z eq {constant})";
                }
                else
                {
                    filter = $"{field.Name} {op} {constant}";
                }
            }
            return filter;
        }

        public static string Constant(object value)
        {
            string constant = null;
            if (value is string)
            {
                constant = $"'{EscapeFilterString(value as string)}'";
            }
            else
            {
                constant = value.ToString();
            }
            return constant;
        }

        private SearchParameters BuildParameters(SearchQueryBuilder queryBuilder, string facet)
        {
            SearchParameters parameters = new SearchParameters
            {
                Top = queryBuilder.HitsPerPage,
                Skip = queryBuilder.PageNumber * queryBuilder.HitsPerPage,
                SearchMode = SearchMode.All
            };

            if (facet != null)
            {
                parameters.Facets = new List<string> { facet };
            }

            if (queryBuilder.Refinements.Count > 0)
            {
                StringBuilder filter = new StringBuilder();
                string separator = string.Empty;

                foreach (var entry in queryBuilder.Refinements)
                {
                    SearchField field;
                    if (Schema.Fields.TryGetValue(entry.Key, out field))
                    {
                        filter.Append(separator);
                        filter.Append(ToFilter(field, entry.Value));
                        separator = " and ";
                    }
                    else
                    {
                        throw new ArgumentException($"{entry.Key} is not in the schema");
                    }
                }

                parameters.Filter = filter.ToString();
            }

            return parameters;
        }

        private static string EscapeFilterString(string s)
        {
            return s.Replace("'", "''");
        }

        public SearchSchema Schema
        {
            get
            {
                return schema;
            }
        }
    }
}
