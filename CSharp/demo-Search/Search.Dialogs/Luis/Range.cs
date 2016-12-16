using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Search.Models;

namespace Search.Dialogs.Luis
{
    public class Range
    {
        public SearchField Property { get; set; }
        public object Lower { get; set; }
        public object Upper { get; set; }
        public bool IncludeLower { get; set; }
        public bool IncludeUpper { get; set; }
        public string Description { get; set; }
    }
}
