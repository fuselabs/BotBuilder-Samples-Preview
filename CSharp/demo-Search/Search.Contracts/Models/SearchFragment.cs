using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    [Serializable]
    public class SearchFragment
    {
        public Synonyms Phrases;
        public SearchSpec Fragment;
    }
}
