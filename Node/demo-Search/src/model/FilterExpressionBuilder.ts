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

    public build(entities: IEntity[], originalText: string, defaultProperty?: string): FilterExpression {
        
        let entityComparisons: ComparisonSpecification[] = [];

        if(entities) {
            for(let entity of entities) {
                if(entity.type == EntityTypes.Comparison) {
                    entityComparisons.push(new ComparisonSpecification(entity));
                }
            }
        }
        
        for(let entity of entities) {
            for(let entityComparison of entityComparisons) {
                entityComparison.addEntity(entity);
            }
        }

        //TODO: Support removals here

        let ranges: IRange[] = [];

        for(let entityComparison of entityComparisons) {
            let range: IRange = this.ranges.resolve(entityComparison, originalText, defaultProperty);
            if(range) {
                ranges.push(range);
            }
        }

        let rangeFilters: FilterExpression = this.expressionFromRanges(ranges, Operator.And);
        let attributeFilters: FilterExpression[] = this.expressionFromAttributes(entities);
        let filter: FilterExpression = this.combineExpressions(attributeFilters, Operator.And, rangeFilters);

        return filter;
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