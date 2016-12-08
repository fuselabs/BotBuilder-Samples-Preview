using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    public enum SortDirection { Descending, Ascending };

#if !NETSTANDARD1_6
    [Serializable]
#else
    [DataContract]
#endif
    public class SortKey
    {
        public SortDirection Direction;
        public string Field;
    }
}
