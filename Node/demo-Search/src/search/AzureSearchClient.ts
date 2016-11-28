
import { ISearchResult, ISearchHit } from './ISearchClient';
import { ISearchSchema } from './ISearchSchema';
import { ISearchQuery } from './ISearchQuery';
import { QueryTypes, SearchModes, SearchApiConstants } from './SearchConstants';
import * as request from 'request';
import * as util from 'util';

export class AzureSearchClient {

    public constructor(private service: string, private adminKey: string, private indexName: string, private searchSchema: ISearchSchema, private mapper: (searchResult: any) => ISearchHit) {

    }

    public search(query: ISearchQuery, callback: (searchResult: ISearchResult, error: Error) => void) {

        const url: string = this.service 
            + SearchApiConstants.indexesApiPath 
            + this.indexName 
            + SearchApiConstants.docsSearchApiPath 
            + SearchApiConstants.azureSearchApiVersion;

        const searchHeaders = {
            'api-key': this.adminKey,
            'Content-Type': SearchApiConstants.jsonContentType
        };

        request.post(url, { body: JSON.stringify(query), headers: searchHeaders}, (error: Error, response: any, body: string) => {
            
            //TODO: Integrate retrying
            //TODO: Promises / async

            if(error) {
                callback(null, error);
            }

            let results = JSON.parse(body);
            let hits: ISearchHit[] = [];
            
            for(let result of results.value) {
                hits.push(this.mapper(result));
            }
            let searchResult: ISearchResult = {
                hits: hits,
                facets: results['@search.facets'] || {}
            }

            callback(searchResult, error);
        });
    }
}

