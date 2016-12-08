using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;

namespace Search.Models
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;


#if !NETSTANDARD1_6
    [Serializable]
#else
    [JsonObject(MemberSerialization.OptOut)]
#endif
    public class SearchSchema
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };

        public Dictionary<string, SearchField> Fields { get; set; }

        public string DefaultCurrencyProperty { get; set; }

        public string DefaultNumericProperty { get; set; }

        public string DefaultGeoProperty { get; set; }

        public List<SearchFragment> Fragments = new List<SearchFragment>();

        public SearchSchema()
        {
            Fields = new Dictionary<string, SearchField>();
        }

        public void AddField(SearchField field)
        {
            Fields.Add(field.Name, field);
        }

        public void RemoveField(string name)
        {
            Fields.Remove(name);
        }

        public SearchField Field(string name)
        {
            return Fields[name];
        }

        public void Save(string path)
        {
            using (var output = new StreamWriter(new FileStream(path, FileMode.OpenOrCreate)))
            {
                output.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public static SearchSchema Load(string path)
        {
            return JsonConvert.DeserializeObject<SearchSchema>(File.ReadAllText(path));
        }
    }
}
