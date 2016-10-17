namespace Search.Models
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class SearchSchema
    {
        private Dictionary<string, SearchField> fields = new Dictionary<string, SearchField>();

        public string DefaultCurrencyProperty { get; set; }

        public string DefaultNumericProperty { get; set; }

        public string DefaultGeoProperty { get; set; }

        public IDictionary<string, SearchField> Fields
        {
            get { return fields; }
        }

        public string CanonicalProperty(string propertyName)
        {
            return propertyName;
        }
    }
}
