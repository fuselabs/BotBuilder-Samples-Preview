using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    [Serializable]
    public class Synonyms
    {
        public string Canonical { get; set; }

        public string[] Alternatives { get; set; }

        public string Description
        {
            get { return Alternatives.FirstOrDefault(); }
        }

        public Synonyms(string canonical, params string[] alternatives)
        {
            Canonical = canonical;
            Alternatives = alternatives;
        }
    }
}
