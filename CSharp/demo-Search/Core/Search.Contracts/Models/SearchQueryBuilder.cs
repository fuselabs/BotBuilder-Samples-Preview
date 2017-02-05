using System.Runtime.Serialization;

namespace Search.Models
{
    using System;
    using System.Collections.Generic;

#if !NETSTANDARD1_6
    [Serializable]
#else
    [DataContract]
#endif
    public class SearchQueryBuilder
    {
        private const int DefaultHitPerPage = 5;
        private const int DefaultMaxFacets = 100;

        public SearchQueryBuilder()
        {
        }

        public SearchSpec Spec = new SearchSpec();

        public int PageNumber { get; set; }

        public int HitsPerPage { get; set; } = DefaultHitPerPage;

        public int MaxFacets { get; set; } = DefaultMaxFacets;

        public SearchQueryBuilder DeepCopy()
        {
            var query = new SearchQueryBuilder();
            query.Spec = this.Spec?.DeepCopy();
            query.HitsPerPage = this.HitsPerPage;
            query.PageNumber = this.PageNumber;
            query.MaxFacets = this.MaxFacets;
            return query;
        }

        public virtual void Reset()
        {
            Spec = new SearchSpec();
            this.PageNumber = 0;
        }

        public virtual bool HasNoConstraints()
        {
            return this.Spec?.Filter == null;
        }
    }
}