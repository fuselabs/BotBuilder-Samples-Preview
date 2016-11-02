namespace Search.Models
{
    using System;
    using System.Linq;
    using System.Text;

    public enum Operator { None, LessThan, LessThanOrEqual, Equal, GreaterThanOrEqual, GreaterThan, And, Or, Not, FullText };

    [Serializable]
    public class FilterExpression
    {
        public readonly Operator Operator;
        public readonly object[] Values;

        public FilterExpression()
        { }

        public FilterExpression(Operator op, params object[] values)
        {
            Operator = op;
            Values = values;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Operator.ToString());
            builder.Append("(");
            var seperator = "";
            foreach (var value in Values)
            {
                builder.Append(seperator);
                builder.Append(value.ToString());
                seperator = ", ";
            }
            builder.Append(")");
            return builder.ToString();
        }

        public FilterExpression DeepCopy()
        {
            var values = (from value in Values select value is FilterExpression ? (object)(value as FilterExpression).DeepCopy() : value).ToArray();
            return new FilterExpression(Operator, values);
        }

        public FilterExpression Remove(SearchField field)
        {
            FilterExpression result = null;
            if (Operator == Operator.And
                || Operator == Operator.Or)
            {
                var child1 = (Values[0] as FilterExpression).Remove(field);
                var child2 = (Values[1] as FilterExpression).Remove(field);
                result = FilterExpression.Combine(child1, child2, Operator);
            }
            else if (Operator == Operator.Not)
            {
                var child = (Values[0] as FilterExpression).Remove(field);
                if (child != null)
                {
                    result = new FilterExpression(Operator.Not, child);
                }
            }
            else
            {
                if (Values.All((v) => (v as SearchField).Name != field.Name))
                {
                    result = this;
                }
            }
            return result;
        }

        public static FilterExpression Combine(FilterExpression child1, FilterExpression child2, Operator combination)
        {
            FilterExpression filter;
            if (child1 != null)
            {
                if (child2 != null)
                {
                    filter = new FilterExpression(combination, child1, child2);
                }
                else
                {
                    filter = child1;
                }
            }
            else
            {
                filter = child2;
            }
            return filter;
        }
    }
}