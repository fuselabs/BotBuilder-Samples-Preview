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

        public AzureSearchClient(SearchSchema schema, IMapper<DocumentSearchResult, GenericSearchResult> mapper)
        {
            this.schema = schema;
            this.mapper = mapper;
            var serviceName = ConfigurationManager.AppSettings["SearchDialogsServiceName"];
            var indexName = ConfigurationManager.AppSettings["SearchDialogsIndexName"];
            var serviceKey = ConfigurationManager.AppSettings["SearchDialogsServiceKey"];
            var client = new SearchServiceClient(serviceName, new SearchCredentials(serviceKey));
            searchClient = client.Indexes.GetClient(indexName);
        }

        public async Task<GenericSearchResult> SearchAsync(SearchQueryBuilder queryBuilder, string refiner)
        {
            var documentSearchResult = await this.searchClient.Documents.SearchAsync(queryBuilder.Spec.Text, BuildParameters(queryBuilder, refiner));

            return this.mapper.Map(documentSearchResult);
        }

        public void AddFields(IEnumerable<Field> fields)
        {
            foreach (var field in fields)
            {
                schema.AddField(SearchTools.ToSearchField(field));
            }
        }

        /*
         * TODO: Remove this
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
        */

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

        private string BuildFilter(FilterExpression expression)
        {
            string filter = "";
            string op = null;
            switch(expression.Operator)
            {
                case Operator.And:
                    {
                        var left = BuildFilter((FilterExpression)expression.Values[0]);
                        var right = BuildFilter((FilterExpression)expression.Values[1]);
                        filter = $"({left}) and ({right})";
                        break;
                    }
                case Operator.Or:
                    {
                        var left = BuildFilter((FilterExpression)expression.Values[0]);
                        var right = BuildFilter((FilterExpression)expression.Values[1]);
                        filter = $"({left}) or ({right})";
                        break;
                    }
                case Operator.Not:
                    {
                        var child = BuildFilter((FilterExpression)expression.Values[0]);
                        filter = $"not ({child})";
                        break;
                    }
                case Operator.FullText:
                    // TODO: What is the right thing here?
                    break;

                case Operator.LessThan: op = "lt"; break;
                case Operator.LessThanOrEqual: op = "le"; break;
                case Operator.Equal: op = "eq"; break;
                case Operator.GreaterThanOrEqual: op = "ge"; break;
                case Operator.GreaterThan: op = "gt"; break;
                default:
                    break;
            }
            if (op != null)
            {
                var field = (SearchField)expression.Values[0];
                var value = Constant(expression.Values[1]);
                if (field.Type == typeof(string[]))
                {
                    if (expression.Operator != Operator.Equal)
                    {
                        throw new NotSupportedException();
                    }
                    filter = $"{field.Name}/any(z: z eq {value})";
                }
                else
                {
                    filter = $"{field.Name} {op} {value}";
                }
            }
            return filter;
        }

        private SearchParameters BuildParameters(SearchQueryBuilder queryBuilder, string facet)
        {
            SearchParameters parameters = new SearchParameters
            {
                Top = queryBuilder.HitsPerPage,
                Skip = queryBuilder.PageNumber * queryBuilder.HitsPerPage,
                SearchMode = SearchMode.Any
            };

            if (facet != null)
            {
                parameters.Facets = new List<string> { facet };
            }

            if (queryBuilder.Spec.Filter != null)
            {
                parameters.Filter = BuildFilter(queryBuilder.Spec.Filter);
            }
            else
            {
                parameters.Filter = null;
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
