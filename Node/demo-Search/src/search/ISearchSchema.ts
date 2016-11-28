
import { ISynonyms } from './ISearchClient';

export interface ISearchSchema {
    Fields?: {[key: string]: ISearchField};
    DefaultCurrencyProperty?: string;
    DefaultNumericProperty?: string;
    DefaultGeoProperty?: string;
    Fragments?: any;
}

export interface ISearchField {
    Name: string;
    Type: string;
    IsFacetable: boolean;
    IsFilterable: boolean;
    IsKey: boolean;
    IsRetrievable: boolean;
    IsSearchable: boolean;
    IsSortable: boolean;
    FilterPreference: string;
    NameSynonyms?: ISynonyms;
    ValueSynonyms?: ISynonyms[];
}