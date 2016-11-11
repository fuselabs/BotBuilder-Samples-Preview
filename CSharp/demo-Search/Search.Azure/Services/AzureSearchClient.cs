﻿namespace Search.Azure.Services
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Search;
    using Microsoft.Azure.Search.Models;
    using Search.Models;
    using Search.Services;
    using System.Text;

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
            var oldFilter = queryBuilder.Spec.Filter;
            if (refiner != null && oldFilter != null)
            {
                queryBuilder.Spec.Filter = queryBuilder.Spec.Filter.Remove(this.Schema.Field(refiner));
            }
            string search;
            var parameters = BuildSearch(queryBuilder, refiner, out search);
            var documentSearchResult = await this.searchClient.Documents.SearchAsync(search, parameters);
            queryBuilder.Spec.Filter = oldFilter;
            return this.mapper.Map(documentSearchResult);
        }

        public void AddFields(IEnumerable<Field> fields)
        {
            foreach (var field in fields)
            {
                schema.AddField(SearchTools.ToSearchField(field));
            }
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

        // Azure search only supports full text in a seperate tree combined with AND of the filter.
        // We extract all of the FullText operators found in the tree along AND paths only.
        private FilterExpression ExtractFullText(FilterExpression expression, List<FilterExpression> searchExpression)
        {
            FilterExpression filter = null;
            if (expression != null)
            {
                switch (expression.Operator)
                {
                    case Operator.And:
                        {
                            var left = ExtractFullText((FilterExpression)expression.Values[0], searchExpression);
                            var right = ExtractFullText((FilterExpression)expression.Values[1], searchExpression);
                            filter = FilterExpression.Combine(left, right);
                        }
                        break;
                    case Operator.FullText:
                        {
                            searchExpression.Add(expression);
                            filter = null;
                        }
                        break;
                    default:
                        filter = expression;
                        break;
                }
            }
            return filter;
        }

        private string BuildFilter(FilterExpression expression)
        {
            string filter = null;
            if (expression != null)
            {
                string op = null;
                switch (expression.Operator)
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
                        throw new ArgumentException("Cannot handle complex full text expressions.");

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
            }
            return filter;
        }

        private SearchParameters BuildSearch(SearchQueryBuilder queryBuilder, string facet, out string search)
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

            var searchExpressions = new List<FilterExpression>();
            var filter = ExtractFullText(queryBuilder.Spec.Filter, searchExpressions);
            parameters.QueryType = QueryType.Full;
            parameters.Filter = BuildFilter(filter);
            search = BuildSearchFilter(queryBuilder.Spec.Text, searchExpressions);
            return parameters;
        }

        private string BuildSearchFilter(string text, IList<FilterExpression> expressions)
        {
            var builder = new StringBuilder();
            string prefix = "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append($"{text} ");
                prefix = " AND ";
            }
            foreach(var expression in expressions)
            {
                var property = (SearchField) expression.Values[0];
                var value = EscapeFilterString((string)expression.Values[1]);
                builder.Append($"{prefix}{property.Name}:'{value}'");
                prefix = " AND ";
            }
            return builder.ToString();
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
