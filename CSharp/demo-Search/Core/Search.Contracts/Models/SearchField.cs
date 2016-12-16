using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Search.Models
{
    public enum PreferredFilter { None, Facet, MinValue, MaxValue };

#if !NETSTANDARD1_6
    [Serializable]
#else
    [JsonObject(MemberSerialization.OptOut)]
#endif
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

        public string Name { get; set; }
        public Type Type { get; set; } = typeof(string);
        public bool IsFacetable { get; set; }
        public bool IsFilterable { get; set; }
        public bool IsKey { get; set; }
        public bool IsRetrievable { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }

        // Fields to control experience
        public PreferredFilter FilterPreference { get; set; }
        public Synonyms NameSynonyms { get; set; }
        public Synonyms[] ValueSynonyms { get; set; } = new Synonyms[0];
    }
}
