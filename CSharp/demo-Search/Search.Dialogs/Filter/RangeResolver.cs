using System;
using Search.Models;
using Search.Utilities;
using Microsoft.Bot.Builder.Luis.Models;
using System.Linq;

namespace Search.Dialogs.Filter
{
    internal class RangeResolver
    {
        private readonly SearchSchema schema;

        public RangeResolver(SearchSchema schema)
        {
            this.schema = schema;
        }

        public Range Resolve(ComparisonEntity c, string originalText, string defaultProperty)
        {
            Range range = null;
            var isLowerCurrency = false;
            var isUpperCurrency = false;
            object lower = c.Lower == null ? double.NegativeInfinity : ParseValue(c.Lower, out isLowerCurrency);
            object upper = c.Upper == null ? double.PositiveInfinity : ParseValue(c.Upper, out isUpperCurrency);
            var isCurrency = isLowerCurrency || isUpperCurrency;

            var propertyName = c.Property?.FirstResolution();
            if (propertyName == null)
            {
                if (isCurrency)
                {
                    propertyName = schema.DefaultCurrencyProperty ?? defaultProperty;
                }
                else
                {
                    propertyName = defaultProperty;
                }
            }

            if (propertyName != null)
            {
                var field = schema.Field(propertyName);
                if (field.Type == typeof(string)
                    || (lower is double && upper is double))
                {
                    range = new Range { Property = field };
                    if (c.Operator == null)
                    {
                        // This is the case where we just have naked values
                        range.IncludeLower = true;
                        range.IncludeUpper = true;
                        upper = lower;
                    }
                    else
                    {
                        switch (c.Operator.FirstResolution())
                        {
                            case ">=":
                                range.IncludeLower = true;
                                range.IncludeUpper = true;
                                upper = double.PositiveInfinity;
                                break;

                            case ">":
                                range.IncludeLower = false;
                                range.IncludeUpper = true;
                                upper = double.PositiveInfinity;
                                break;

                            case "between":
                                range.IncludeLower = true;
                                range.IncludeUpper = true;
                                break;

                            case "<=":
                                range.IncludeLower = true;
                                range.IncludeUpper = true;
                                upper = lower;
                                lower = double.NegativeInfinity;
                                break;

                            case "<":
                                range.IncludeLower = true;
                                range.IncludeUpper = false;
                                upper = lower;
                                lower = double.NegativeInfinity;
                                break;

                            case "any":
                                upper = double.PositiveInfinity;
                                lower = double.NegativeInfinity;
                                break;

                            default:
                                throw new ArgumentException($"Unknown operator {c.Operator.Entity}");
                        }
                    }
                    range.Lower = lower;
                    range.Upper = upper;
                    range.Description = c.Entity?.Entity;
                }
            }
            return range;
        }

        private object ParseValue(EntityRecommendation entity, out bool isCurrency)
        {
            object result = ParseNumber(entity.Entity, out isCurrency);
            if (result is double && double.IsNaN((double) result))
            {
                result = entity.Entity;
            }
            return result;
        }

        private double ParseNumber(string entity, out bool isCurrency)
        {
            isCurrency = false;
            var multiply = 1.0;
            if (entity.StartsWith("$"))
            {
                isCurrency = true;
                entity = entity.Substring(1);
            }
            if (entity.EndsWith("k"))
            {
                multiply = 1000.0;
                entity = entity.Substring(0, entity.Length - 1);
            }
            double result;
            var str = entity.Replace(",", "").Replace(" ", "");
            if (double.TryParse(str, out result))
            {
                result *= multiply;
            }
            else
            {
                result = double.NaN;
            }
            return result;
        }
    }
}