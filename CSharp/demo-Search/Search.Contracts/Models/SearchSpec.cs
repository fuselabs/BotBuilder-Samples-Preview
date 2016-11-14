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
        public List<string> Phrases = new List<string>();
        public FilterExpression Filter = null;
        public List<SortKey> Sort = new List<SortKey>();
        public List<string> Selection = new List<string>();
        public int? Skip;
        public int? Top;

        public void Remove(SearchField field)
        {
            if (Filter != null)
            {
                Filter = Filter.Remove(field);
            }
            Sort.RemoveAll((s) => s.Field == field.Name);
        }

        public void Merge(SearchSpec other, Operator filterCombine)
        {
            this.Phrases = this.Phrases.Union(other.Phrases).ToList();
            if (Filter == null)
            {
                this.Filter = other.Filter;
            }
            else if (other.Filter != null)
            {
                this.Filter = new FilterExpression(filterCombine, this.Filter, other.Filter);
            }
            this.Sort.AddRange(other.Sort);
            this.Selection.AddRange(other.Selection);
            this.Skip = Merge(this.Skip, other.Skip);
            this.Top = Merge(this.Top, other.Top);
        }

        public SearchSpec DeepCopy()
        {
            return new SearchSpec
            {
                Filter = this.Filter?.DeepCopy(),
                Phrases = this.Phrases.ToList(),
                Sort = this.Sort.ToList(),
                Selection = this.Selection.ToList(),
                Skip = this.Skip,
                Top = this.Top
            };
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
