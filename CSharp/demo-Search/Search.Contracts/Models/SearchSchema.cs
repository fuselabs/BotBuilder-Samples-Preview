namespace Search.Models
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;

    [Serializable]
    public class SearchSchema
    {
        private Dictionary<string, SearchField> fields = new Dictionary<string, SearchField>();

        public string DefaultCurrencyProperty { get; set; }

        public string DefaultNumericProperty { get; set; }

        public string DefaultGeoProperty { get; set; }

        public List<SearchFragment> Fragments = new List<SearchFragment>();

        public void AddField(SearchField field)
        {
            fields.Add(field.Name, field);
        }

        public void RemoveField(string name)
        {
            fields.Remove(name);
        }

        public SearchField Field(string name)
        {
            return fields[name];
        }

        public IReadOnlyDictionary<string, SearchField> Fields
        {
            get { return fields; }
        }

        public void Save(string path)
        {
            using (var output = new StreamWriter(path))
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
