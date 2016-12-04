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

import { IEntity } from 'botbuilder';
import { ISearchSpecification } from '../dialogs/SearchSpecificationManager';
import { ISearchField, ISearchSchema } from '../search/ISearchSchema';
import { FilterExpression, Operator } from './FilterExpression';
import { ICanonicalValue } from './Canonicalizer';
import { IDialogCanonicalizers } from './CanonicalizerBuilder';
import { Normalizer } from './Normalizer';
import { EntityTypes } from './EntityTypes';
import { IRange, Ranges } from './Ranges';
import { ComparisonSpecification } from './ComparisonSpecification';

export class FilterExpressionBuilder {

    private readonly ranges: Ranges;

    public constructor(private canonicalizers: IDialogCanonicalizers, private schema: ISearchSchema) {
        this.ranges = new Ranges(schema, canonicalizers);
    }

    public build(entities: IEntity[], originalText: string, defaultProperty?: string, currentFilter?: FilterExpression): FilterExpression {
        
        let entityComparisons: ComparisonSpecification[] = [];
        let removals: IEntity[] = [];

        // Identify entities of type comparison and of type removal
        if(entities) {
            for(let entity of entities) {
                if(entity.type == EntityTypes.Comparison) {
                    entityComparisons.push(new ComparisonSpecification(entity));
                }

                if(entity.type == EntityTypes.Removal) {
                    removals.push(entity);
                }
            }
        }
        
        // Link other entities (operator, value, etc) to the comparison entities
        for(let entity of entities) {
            for(let entityComparison of entityComparisons) {
                entityComparison.addEntity(entity);
            }
        }

        // Process removals
        for(let removal of removals) {
            for(let entity of entities) {
                if(entity.type == EntityTypes.Property && entity.startIndex >= removal.startIndex && entity.endIndex <= removal.endIndex) {
                    
                    if(currentFilter) {
                        let canonical = this.canonicalizers.fieldCanonicalizer.canonicalize(entity.entity);
                        
                        if(canonical) {
                            let searchField: ISearchField = this.schema.Fields[canonical];

                            if(searchField) {
                                currentFilter = currentFilter.removeSearchField(searchField);
                            }
                        }
                    }
                }
            }
        }

        // Identify ranges that will apply to comparisons
        let ranges: IRange[] = [];

        for(let entityComparison of entityComparisons) {
            let range: IRange = this.ranges.resolve(entityComparison, originalText, defaultProperty);
            if(range) {
                ranges.push(range);
            }
        }

        // Compute filter for comparison ranges
        let rangeFilters: FilterExpression = this.expressionFromRanges(ranges, Operator.And);

        // Compute filter for attributes
        let attributeFilters: FilterExpression[] = this.expressionFromAttributes(entities);

        // Combine attribute and ranges filters
        let attributeAndRangeFilter: FilterExpression = this.combineExpressions(attributeFilters, Operator.And, rangeFilters);

        // Combine with the already existing filter if it existed, otherwise return the new filter
        let combinedFilter: FilterExpression;

        if(currentFilter) {
             combinedFilter = FilterExpression.combine(
                currentFilter.remove(attributeAndRangeFilter),
                attributeAndRangeFilter,
                Operator.And
            );   
        } else {
            combinedFilter = attributeAndRangeFilter;
        }

        return combinedFilter;
    }

    private combineExpressions(filters: FilterExpression[], operator: Operator = Operator.And, soFar: FilterExpression = null) {
        let result: FilterExpression = soFar;
        for(let filter of filters) {
            result = FilterExpression.combine(result, filter, operator);
        }

        return result;
    }

    private expressionFromRanges(ranges: IRange[], operator: Operator = Operator.And, soFar: FilterExpression = null): FilterExpression {
        let filter: FilterExpression = soFar;

        for(let range of ranges) {
            let lowerComparison = range.includeLower ? Operator.GreaterThanOrEqual : Operator.GreaterThan;
            let upperComparison = range.includeUpper ? Operator.LessThanOrEqual : Operator.LessThan;

            if(range.lower == Number.NEGATIVE_INFINITY) {
                if(range.upper != Number.POSITIVE_INFINITY) {
                    filter = FilterExpression.combine(filter, 
                        new FilterExpression(range.description, upperComparison, range.property, range.upper), operator
                    );
                }
            }
            else if(range.upper == Number.POSITIVE_INFINITY) {
                filter = FilterExpression.combine(filter, 
                    new FilterExpression(range.description, lowerComparison, range.property, range.lower), operator
                ); 
            }
            else if(range.upper == range.lower) {
                filter = FilterExpression.combine(filter, 
                    new FilterExpression(range.description, 
                        range.lower instanceof String && range.property.IsSearchable ? Operator.FullText : Operator.Equal, 
                        range.property, 
                        range.lower), 
                    operator
                );       
            }
            else {
                // Only add the description to the combination to avoid description duplication and limit tree traversal when computing the string representation
                let child = FilterExpression.combine(
                    new FilterExpression(null, lowerComparison, range.property, range.lower),
                    new FilterExpression(null, upperComparison, range.property, range.upper), 
                    Operator.And,
                    range.description
                );
                filter = FilterExpression.combine(filter, child, operator);
            }
        }

        return filter;
    }

    private expressionFromAttributes(entities: IEntity[]): FilterExpression[] {

        let expressions: FilterExpression[] = [];

        if(entities) {
            for(let entity of entities) {
                let canonical = this.canonicalAttribute(entity);
                if(canonical) {
                    expressions.push(new FilterExpression(entity.entity, Operator.Equal, canonical.field, canonical.value));
                }
            }
        }
        return expressions;
    }

    private canonicalAttribute(entity: IEntity): ICanonicalValue {
        if(entity.type != 'Attribute') {
            return null;
        }

        return this.canonicalizers.valueCanonicalizers[Normalizer.normalize(entity.entity)];
    }
}