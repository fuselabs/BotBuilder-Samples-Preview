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

import { FilterExpression, Operator } from '../model/FilterExpression';
import { ISearchQuery } from './ISearchQuery';
import { ISearchField } from './ISearchSchema';
import { QueryTypes, SearchModes, SearchDefaults } from './SearchConstants';
import { ISearchSpecification } from '../dialogs/SearchSpecificationManager';
import { StringBuilder } from '../tools/StringBuilder';
import { sprintf } from 'sprintf-js';

export interface IExpressionAndFullTextFilter {
    expressionFilter: FilterExpression;
    fullTextFilter: FilterExpression[];
}

export class SearchQueryBuilder {

    constructor() {}

    public build(searchSpec: ISearchSpecification): ISearchQuery {
        
        const facetFormat = '%s,count:%d';

        let query: ISearchQuery = {
            top: searchSpec.top || SearchDefaults.pageSize,
            count: true,
            facets: [],
            filter: '',
            queryType: QueryTypes.full,
            search: '',
            searchFields: '',
            searchMode: SearchModes.any,
            skip: (searchSpec.pageNumber || 0) * (searchSpec.top || SearchDefaults.pageSize)
        };

        if(searchSpec.facet) {
            query.facets.push(sprintf(facetFormat, searchSpec.facet, SearchDefaults.maxFacets));
        }

        // Build separate filters for expressions (e.g. # beds > 2) and full text (e.g. 'fireplace')
        let segregatedFilters: IExpressionAndFullTextFilter = this.segregateFullTextFilters(searchSpec);

        if(segregatedFilters) {
            // Compute the query string for the expression part of the filter
            query.filter = this.computeExpressionQuery(segregatedFilters.expressionFilter);

            // Compute the query string for the full text part of the filter
            query.search = this.computeFullTextQuery(searchSpec.phrases, segregatedFilters.fullTextFilter);
        }

        return query;
    }

    private segregateFullTextFilters(searchSpec: ISearchSpecification): IExpressionAndFullTextFilter {

        if(!searchSpec.filter) return null;

        let fullTextFilter = new Array<FilterExpression>(); 
        let expressionFilter = FilterExpression.traversePostOrder<FilterExpression>(searchSpec.filter, (node: FilterExpression, childrenResults: FilterExpression[]): FilterExpression => {

            switch(node.getOperator()) {
                case Operator.And:
                    return FilterExpression.combine(childrenResults[0], childrenResults[1]);
                case Operator.FullText:
                    fullTextFilter.push(node);
                    return null;
                default:
                    return node;
            }
        });
        let result: IExpressionAndFullTextFilter = {
            expressionFilter: expressionFilter,
            fullTextFilter: fullTextFilter
        };
        return result;
    }

    private computeExpressionQuery(filter: FilterExpression): string {

        if(!filter) return '';

        return FilterExpression.traversePostOrder<string>(filter, (node: FilterExpression, childrenResults: string[]): string => {
            let op: string = null;

            switch(node.getOperator()) {
                case Operator.And:
                    return sprintf('(%s) and (%s)', childrenResults[0], childrenResults[1]);
                case Operator.Or:
                    return sprintf('(%s) or (%s)', childrenResults[0], childrenResults[1]);
                case Operator.Not:
                    return sprintf('not (%s)', childrenResults[0]);
                
                case Operator.FullText:
                    throw new Error('Full text expressions are not supported');

                case Operator.LessThan: op = 'lt'; break;
                case Operator.LessThanOrEqual: op = 'le'; break;
                case Operator.Equal: op = 'eq'; break;
                case Operator.GreaterThanOrEqual: op = 'ge'; break;
                case Operator.GreaterThan: op = 'gt'; break;
                default: break;
            }
            
            if(op != null) {
                let searchField: ISearchField = node.getValues()[0];
                let value = this.textConstantFrom(node.getValues()[1]);

                //TODO: support string[]
                //if(searchField.Type == <CLR STRING TYPE HERE>) {
                //    if(node.getOperator() != Operator.Equal) throw new Error('String array only supports equal operator');
                //    return util.format('%s/any(z: z eq %s)', searchField.Name, value);
                //}

                return sprintf('%s %s %s', searchField.Name, op, value);
            }

            return null;
        });
    }

    private computeFullTextQuery(phrases: string[], expressions: FilterExpression[]) {
        const orPrefix = ' OR ';
        const andPrefix = ' AND ';
        const emptyPrefix = '';
        const expressionStartFormat = '%s(';
        const expressionFormat = '%s%s:%s';
        const expressionEnd = ')';

        let prefix = emptyPrefix;
        let stringBuilder = new StringBuilder()

        //Query for phrases
        for(let phrase of phrases) {
            stringBuilder.append(prefix);
            stringBuilder.append(this.textConstantFrom(phrase));
            if(prefix == emptyPrefix) {
                prefix = orPrefix;
            }
        }

        if(expressions && expressions.length > 0) {
            stringBuilder.append(sprintf(expressionStartFormat, prefix));
            prefix = emptyPrefix;

            for(let expression of expressions) {
                let property: ISearchField = <ISearchField>expression.getValues()[0];
                let value = this.textConstantFrom(expression.getValues()[1]);

                stringBuilder.append(sprintf(expressionFormat, prefix, property.Name, value));
                if(prefix == emptyPrefix) {
                    prefix = andPrefix;
                }
            }
            stringBuilder.append(expressionEnd);
        }

        return stringBuilder.toString();
    }

    private textConstantFrom(value: any): string {
        if(typeof value == 'string') {
            let escapedValue = <string>value.replace('\'','\'\'');
            return sprintf('\'%s\'', escapedValue);
        }
        //TODO Datetime / Offset support
        return value;
    }

}

