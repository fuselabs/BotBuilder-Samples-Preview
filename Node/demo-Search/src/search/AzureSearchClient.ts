// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Bot Framework: http://botframework.com
// 
// Bot Builder SDK Github:
// https://github.com/Microsoft/BotBuilder
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

import { ISearchResult, ISearchHit } from './ISearchClient';
import { ISearchSchema } from './ISearchSchema';
import { ISearchQuery } from './ISearchQuery';
import { QueryTypes, SearchModes, SearchApiConstants } from './SearchConstants';
import * as request from 'request';
import * as sprintf from 'sprintf-js';
import * as url from 'url';

export class AzureSearchClient {

    public constructor(private service: string, private adminKey: string, private indexName: string, private searchSchema: ISearchSchema, private mapper: (searchResult: any) => ISearchHit) {}
    
    public search(query: ISearchQuery, callback: (error: Error, searchResult: ISearchResult) => void) {

        const urlStr: string = this.service 
            + SearchApiConstants.indexesApiPath 
            + this.indexName 
            + SearchApiConstants.docsSearchApiPath 
            + SearchApiConstants.azureSearchApiVersion;

        const urlObj = url.parse(urlStr, true);

        const searchHeaders = {
            'api-key': this.adminKey,
            'Content-Type': SearchApiConstants.jsonContentType
        };

        request.post(url.format(urlObj), { body: JSON.stringify(query), headers: searchHeaders}, (error: Error, response: any, body: string) => {

            if(error) {
                callback(error, null);
            }

            let results = JSON.parse(body);
            let hits: ISearchHit[] = [];
            let facets: any = {};
            if(results && results.value) {
                for(let result of results.value) {
                    hits.push(this.mapper(result));
                }

                facets = results['@search.facets'] || {};
            }
            let searchResult: ISearchResult = {
                hits: hits,
                facets: facets 
            };
            callback(error, searchResult);
        });
    }
}

