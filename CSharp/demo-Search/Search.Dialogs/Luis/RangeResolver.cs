using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Search.Models;

namespace Search.Dialogs.Luis
{
    class RangeResolver
    {
        private readonly SearchSchema schema;
        private readonly Canonicalizer fieldCanonicalizer;

        public RangeResolver(SearchSchema schema, Canonicalizer fieldCanonicalizer)
        {
            this.schema = schema;
            this.fieldCanonicalizer = fieldCanonicalizer;
        }

        public Range Resolve(ComparisonEntity c, string originalText, string defaultProperty)
        {
            Range range = null;
            bool isCurrency = false;

            object lower = c.Lower == null ? double.NegativeInfinity : ParseNumber(c.Lower.Entity, out isCurrency);
            object upper = c.Upper == null ? double.PositiveInfinity : ParseNumber(c.Upper.Entity, out isCurrency);

            string propertyName = c.Property?.Entity;

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
            else
            {
                propertyName = this.fieldCanonicalizer.Canonicalize(c.Property.Entity);
            }

            if (propertyName != null)
            {
                range = new Range { Property = schema.Field(propertyName) };
                if (lower is double && double.IsNaN((double)lower))
                {
                    lower = originalText.Substring(c.Lower.StartIndex.Value, c.Lower.EndIndex.Value - c.Lower.StartIndex.Value + 1);
                }
                if (upper is double && double.IsNaN((double)upper))
                {
                    upper = originalText.Substring(c.Upper.StartIndex.Value, c.Upper.EndIndex.Value - c.Upper.StartIndex.Value + 1);
                }
                if (c.Operator == null)
                {
                    // This is the case where we just have naked values
                    range.IncludeLower = true;
                    range.IncludeUpper = true;
                    upper = lower;
                }
                else
                {
                    switch (c.Operator.Entity)
                    {
                        case ">=":
                        case "+":
                        case "greater than or equal":
                        case "at least":
                        case "no less than":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            upper = double.PositiveInfinity;
                            break;

                        case ">":
                        case "greater than":
                        case "more than":
                            range.IncludeLower = false;
                            range.IncludeUpper = true;
                            upper = double.PositiveInfinity;
                            break;

                        case "-":
                        case "between":
                        case "and":
                        case "or":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            break;

                        case "<=":
                        case "no more than":
                        case "less than or equal":
                            range.IncludeLower = true;
                            range.IncludeUpper = true;
                            upper = lower;
                            lower = double.NegativeInfinity;
                            break;

                        case "<":
                        case "less than":
                            range.IncludeLower = true;
                            range.IncludeUpper = false;
                            upper = lower;
                            lower = double.NegativeInfinity;
                            break;

                        case "any":
                        case "any number of":
                            upper = double.PositiveInfinity;
                            lower = double.NegativeInfinity;
                            break;

                        default: throw new ArgumentException($"Unknown operator {c.Operator.Entity}");
                    }
                }
                range.Lower = lower;
                range.Upper = upper;
                range.Description = c.Entity?.Entity;
            }
            return range;
        }

        private double ParseNumber(string entity, out bool isCurrency)
        {
            isCurrency = false;
            double multiply = 1.0;
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
