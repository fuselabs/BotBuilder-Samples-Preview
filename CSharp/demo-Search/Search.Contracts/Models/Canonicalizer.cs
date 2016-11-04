using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Models
{
    [Serializable]
    public class Canonicalizer
    {
        private Dictionary<string, Synonyms> map = new Dictionary<string, Synonyms>();

        private string Normalize(string source)
        {
            return source.Trim().ToLower();
        }

        public Canonicalizer(params Synonyms[] synonyms)
        {
            foreach (var synonym in synonyms)
            {
                Add(synonym);
            }
        }

        public void Add(Synonyms synonyms)
        {
            foreach (var alt in synonyms.Alternatives)
            {
                map.Add(Normalize(alt), synonyms);
            }
        }

        public void Remove(Synonyms synonyms)
        {
            map.Remove(Normalize(synonyms.Canonical));
            foreach (var alt in synonyms.Alternatives)
            {
                map.Remove(Normalize(alt));
            }
        }

        public string Canonicalize(string source)
        {
            string canonical = null;
            Synonyms synonyms;
            if (source != null && map.TryGetValue(Normalize(source), out synonyms))
            {
                canonical = synonyms.Canonical;
            }
            return canonical;
        }
    }
}
