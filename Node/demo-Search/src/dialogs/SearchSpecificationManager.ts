
import { ISearchSchema } from '../search/ISearchSchema';
import { IDialogCanonicalizers, CanonicalizerBuilder } from '../model/CanonicalizerBuilder';
import { FilterExpressionBuilder } from '../model/FilterExpressionBuilder';
import { FilterExpression, Operator } from '../model/FilterExpression';
import { IEntity } from 'botbuilder'; 

export interface ISearchSpecification {
    phrases: string[];
    filter: FilterExpression;
    selection: string[];
    skip?: number;
    top?: number;
    facet?: string;
}

export class SearchSpecificationManager {

    private searchSpec: ISearchSpecification;
    private readonly canonicalizers: IDialogCanonicalizers;
    private readonly filterExpressionBuilder: FilterExpressionBuilder;

    public constructor(private searchSchema: ISearchSchema) {
        this.canonicalizers = CanonicalizerBuilder.build(searchSchema);
        this.filterExpressionBuilder = new FilterExpressionBuilder(this.canonicalizers, searchSchema);
    }

    public filter(entities: IEntity[], originalText: string, defaultProperty?: string): void {
       
        // Compute new filter
        let newFilter: FilterExpression = this.filterExpressionBuilder.build(entities, originalText, defaultProperty);
        
        // Combine filters
        if(this.searchSpec.filter) {
            this.searchSpec.filter = FilterExpression.combine(
                this.searchSpec.filter.remove(newFilter),
                newFilter,
                Operator.And
            );   
        }
        else {
            this.searchSpec.filter = newFilter;
        }
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
            skip: 0,
            top: 5
        };
    }
}