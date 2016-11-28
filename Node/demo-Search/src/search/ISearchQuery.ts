
//from https://docs.microsoft.com/en-us/rest/api/searchservice/search-documents
export interface ISearchQuery {
    count: boolean;
    facets: string[];
    filter: string;
    search: string;
    searchFields: string;
    searchMode: string;
    skip: number;
    top: number;
    queryType: string;
}
