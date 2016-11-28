
import { FilterExpression, Operator } from '../model/FilterExpression';
import { ISearchQuery } from './ISearchQuery';
import { ISearchField } from './ISearchSchema';
import { QueryTypes, SearchModes } from './SearchConstants';
import { ISearchSpecification } from '../dialogs/SearchSpecificationManager';

import * as util from 'util';

export interface IExpressionAndFullTextFilter {
    expressionFilter: FilterExpression;
    fullTextFilter: FilterExpression[];
}

export class SearchQueryBuilder {

    constructor() {}

    public build(searchSpec: ISearchSpecification): ISearchQuery {
        
        const facetFormat = '%s,count:%d';

        let query: ISearchQuery = {
            top: searchSpec.top || 5,
            count: true,
            facets: [],
            filter: '',
            queryType: QueryTypes.full,
            search: '',
            searchFields: '',
            searchMode: SearchModes.any,
            skip: searchSpec.skip
        };

        if(searchSpec.facet) {
            query.facets.push(util.format(facetFormat, searchSpec.facet, 100));//TODO: max facets constant
        }

        let segregatedFilters: IExpressionAndFullTextFilter = this.segregateFullTextFilters(searchSpec);
        let expressionQuery: string = this.computeExpressionQuery(segregatedFilters.expressionFilter);
        let fullTextQuery: string = this.computeFullTextQuery(searchSpec.phrases, segregatedFilters.fullTextFilter);

        query.filter = expressionQuery;
        query.search = fullTextQuery;

        return query;
    }

    private segregateFullTextFilters(searchSpec: ISearchSpecification): IExpressionAndFullTextFilter {

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

        return FilterExpression.traversePostOrder<string>(filter, (node: FilterExpression, childrenResults: string[]): string => {
            let op: string = null;

            switch(node.getOperator()) {
                case Operator.And:
                    return util.format('(%s) and (%s)', childrenResults[0], childrenResults[1]);
                case Operator.Or:
                    return util.format('(%s) or (%s)', childrenResults[0], childrenResults[1]);
                case Operator.Not:
                    return util.format('not (%s)', childrenResults[0]);
                
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

                return util.format('%s %s %s', searchField.Name, op, value);
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
        let stringBuilder: string[] = [];

        //Query for phrases
        for(let phrase of phrases) {
            stringBuilder.push(prefix);
            stringBuilder.push(this.textConstantFrom(phrase));
            if(prefix == emptyPrefix) {
                prefix = orPrefix;
            }
        }

        if(expressions && expressions.length > 0) {
            stringBuilder.push(util.format(expressionStartFormat, prefix));
            prefix = emptyPrefix;

            for(let expression of expressions) {
                let property: ISearchField = <ISearchField>expression.getValues()[0];
                let value = this.textConstantFrom(expression.getValues()[1]);

                stringBuilder.push(expressionFormat, prefix, property.Name, value);
                if(prefix == emptyPrefix) {
                    prefix = andPrefix;
                }
            }
            stringBuilder.push(expressionEnd);
        }

        return stringBuilder.join(emptyPrefix);
    }

    private textConstantFrom(value: any): string {
        if(typeof value == 'string') {
            let escapedValue = <string>value.replace('\'','\'\'');
            return util.format('\'%s\'', escapedValue);
        }
        //TODO Datetime / Offset support
        return value;
    }

}

