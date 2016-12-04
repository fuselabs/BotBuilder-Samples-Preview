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

import { ISearchSchema } from '../search/ISearchSchema';
import { IFacet } from '../search/ISearchClient';
import { IDialogCanonicalizers, CanonicalizerBuilder } from '../model/CanonicalizerBuilder';
import { FilterExpressionBuilder } from '../model/FilterExpressionBuilder';
import { FilterExpression, Operator } from '../model/FilterExpression';
import { Keywords } from '../model/Keywords';
import { IEntity, IRecognizeContext } from 'botbuilder'; 

export enum SortDirections {
    ascending,
    descending
}

export interface ISortKey {
    sortDirection: SortDirections;
    field: string;
}

export interface ISearchSpecification {
    phrases: string[];
    filter: FilterExpression;
    selection: string[];
    pageNumber?: number;
    top?: number;
    facet?: string;
    sort?: ISortKey[];
}

export class SearchSpecificationManager {

    private readonly filterExpressionBuilder: FilterExpressionBuilder;

    private searchSpec: ISearchSpecification;
    private refiner: string;

    public constructor(private searchSchema: ISearchSchema, private canonicalizers: IDialogCanonicalizers) {
        this.filterExpressionBuilder = new FilterExpressionBuilder(this.canonicalizers, searchSchema);
        this.searchSpec = this.getDefaultSpec();
    }

    public filter(context: IRecognizeContext, entities: IEntity[], originalText: string, defaultProperty?: string): void {
       
        // Compute new filter
        this.searchSpec.filter = this.filterExpressionBuilder.build(entities, originalText, defaultProperty, this.searchSpec.filter);

        // Extract keywords that were not included in the expression filter
        let keywords = new Keywords(context);
        this.searchSpec.phrases = keywords.phrases(entities, originalText);
    }

    public facet(entities: IEntity[], originalText: string, defaultProperty?: string): void {
        
        if(entities && entities.length > 0) {
            let query: string = entities[0].entity;
            this.searchSpec.facet = this.canonicalizers.fieldCanonicalizer.canonicalize(query);
        } 
    }

    public nextPage() {
        this.searchSpec.pageNumber++;
    }

    public reset() {
        this.searchSpec = this.getDefaultSpec();
    }

    public getSpec(): ISearchSpecification {
        if(!this.searchSpec) {
            this.searchSpec = this.getDefaultSpec();
        }
        
        return this.searchSpec;
    }

    public getDefaultSpec(): ISearchSpecification {
        return {
            facet: '',
            phrases: [],
            filter: null,
            selection: [],
            pageNumber: 0,
            top: 5
        };
    }
}