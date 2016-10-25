using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    public enum SortDirection { Descending, Ascending };

    public class SortKey
    {
        public SortDirection Direction;
        public string Field;
    }
}
