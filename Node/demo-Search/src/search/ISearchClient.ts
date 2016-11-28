
import { ISearchQuery } from './ISearchQuery';
import { FilterExpression } from '../model/FilterExpression';
import * as request from 'request';

export interface ISearchClient {
    search(query: ISearchQuery, callback: (searchResult: ISearchResult, error: Error) => void): void;
}

export interface ISynonyms {
    Canonical: string;
    Alternatives: string[];
}

export interface IFacet {
    count: number;
    value: string;
}

export interface ISearchHit {
    key: string;
    title: string;
    thumbnailUrl: string;
    description:string;
}

export interface ISearchResult {
    hits?: ISearchHit[];
    facets?: {[key: string]: IFacet[]};
}




