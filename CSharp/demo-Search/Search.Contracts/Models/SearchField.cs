using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    public enum PreferredFilter { None, Facet, MinValue, MaxValue };

    [Serializable]
    public class SearchField
    {
        public SearchField(string name, params string[] alternatives)
        {
            Name = name;
            NameSynonyms = new Synonyms(name, alternatives);
        }

        public override string ToString()
        {
            return Name;
        }

        public string Description()
        {
            return NameSynonyms.Alternatives.First();
        }

        public string Name;
        public Type Type = typeof(string);
        public bool IsFacetable;
        public bool IsFilterable;
        public bool IsKey;
        public bool IsRetrievable;
        public bool IsSearchable;
        public bool IsSortable;

        // Fields to control experience
        public PreferredFilter FilterPreference;
        public Synonyms NameSynonyms;
        public Synonyms[] ValueSynonyms = new Synonyms[0];
    }
}
