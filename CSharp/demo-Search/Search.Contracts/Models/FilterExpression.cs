using System;
using System.Text;

namespace Search.Models
{
    public enum Operator { None, LessThan, LessThanOrEqual, Equal, GreaterThanOrEqual, GreaterThan, And, Or, FullText };

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
    }
}