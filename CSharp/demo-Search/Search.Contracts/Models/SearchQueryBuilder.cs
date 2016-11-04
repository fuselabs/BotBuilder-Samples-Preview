namespace Search.Models
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class SearchQueryBuilder
    {
        private const int DefaultHitPerPage = 5;

        public SearchQueryBuilder()
        {
        }

        public SearchSpec Spec = new SearchSpec();

        public int PageNumber { get; set; }

        public int HitsPerPage { get; set; } = DefaultHitPerPage;

        public virtual void Reset()
        {
            Spec = new SearchSpec();
            this.PageNumber = 0;
        }
    }
}