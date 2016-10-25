using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    [Serializable]
    public class SearchSpec
    {
        public string Text = null;
        public FilterExpression Filter = null;
        public List<SortKey> Sort = new List<SortKey>();
        public List<string> Selection = new List<string>();
        public int? Skip;
        public int? Top;

        public void Merge(SearchSpec other, Operator filterCombine)
        {
            Text = Text ?? "" + " " + other.Text ?? "";
            if (Filter == null)
            {
                Filter = other.Filter;
            }
            else if (other.Filter != null)
            {
                Filter = new FilterExpression(filterCombine, Filter, other.Filter);
            }
            Sort.AddRange(other.Sort);
            Selection.AddRange(other.Selection);
            Skip = Merge(Skip, other.Skip);
            Top = Merge(Top, other.Top);
        }

        private int? Merge(int? current, int? newVal)
        {
            int? result = current;
            if (current.HasValue)
            {
                if (newVal.HasValue)
                {
                    result = Math.Max(current.Value, newVal.Value);
                }
            }
            else if (newVal.HasValue)
            {
                result = newVal;
            }
            return result;
        }
    }
}
