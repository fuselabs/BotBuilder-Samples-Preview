
import { ISearchField } from '../search/ISearchSchema';
import * as util from 'util';

export enum Operator {
    None, LessThan, LessThanOrEqual, Equal, GreaterThanOrEqual, GreaterThan, And, Or, Not, FullText
}

export class FilterExpression {

    private readonly values: any[];

    constructor(private description: string, private operator: Operator,  ...args:any[]) {
        this.values = args;
    }

    public static combine(left: FilterExpression, right: FilterExpression, operator: Operator = Operator.And, description: string = null): FilterExpression {
        let expression: FilterExpression;

        if(left && right) {
            expression = new FilterExpression(description, operator, left, right);
        } else if (left) {
            expression = left;
        } else if (right) {
            expression = right;
        }
        return expression;
    }

    public remove(expression: FilterExpression): FilterExpression {

        let filter: FilterExpression = this;

        if(expression) {
            for(let field of expression.retrieveSearchFields()) {
                filter = filter.removeSearchField(field);
            }
        }

        return filter;
    }

    public retrieveSearchFields(): ISearchField[] {
        
        let fields = new Array<ISearchField>(); 

        // Recursively retrieve all distinct search fields from the tree.
        FilterExpression.traversePreOrder(this, (node: FilterExpression): boolean => {

            if(node.operator == Operator.And || node.operator == Operator.Or || node.operator == Operator.Not) {
                return true;
            }
            else {
                for(let value of node.values) {
                    if(this.isSearchField(value)) {
                        fields.push(value);
                    }
                }
                return false;
            }
        });

        //Rudimentary way of removing duplicates
        return fields.filter((value: ISearchField, index: number, array: ISearchField[]): any => {
             return array.lastIndexOf(value) == index
         });
    }

    public removeSearchField(fieldToRemove: ISearchField): FilterExpression {
        
        let resultExpression: FilterExpression = null; 

        // Recursively remove all occurrences of the specified search field from the expression tree.
        FilterExpression.traversePostOrder<FilterExpression>(this, (node: FilterExpression, childrenResults: FilterExpression[]): FilterExpression => {

            if(node.operator == Operator.And || node.operator == Operator.Or) {
                resultExpression = FilterExpression.combine(childrenResults[0], childrenResults[1], node.operator, node.description);
            }
            else if (node.operator == Operator.Not) {
                if(childrenResults.length == 1 && childrenResults[0]) {
                    resultExpression = new FilterExpression(node.description, Operator.Not, childrenResults[0]);
                }
            }
            else {
                if(node.values.every((value: any, index: number, array: any[]): boolean => {
                    if(this.isSearchField(value)) {
                        if((<ISearchField>value).Name == fieldToRemove.Name) {
                            return false;
                        }
                    }
                    return true;
                }, this)) {
                    resultExpression = node;
                }
            }

            return resultExpression;
        });
        return resultExpression;
    }

    public toUserFriendlyString(): string {
        const spacePrefix = ' ';
        const emptyPrefix = '';
        const expressionFormat = '%s\"%s\"';
        let prefix = emptyPrefix;
        let stringBuilder: string[] = [];
        
        FilterExpression.traversePreOrder(this, (node: FilterExpression): boolean => {
            if(!node.description) {
                return true;
            } 

            stringBuilder.push(util.format(expressionFormat, prefix, node.description));

            if(prefix == emptyPrefix) {
                prefix = spacePrefix;
            }

            return false;
        });

        return stringBuilder.join('');
    }

    private isSearchField(value: any): boolean {
        //TODO: Improve this
        return value.Name && value.Type;
    }

    public static traversePreOrder(node: FilterExpression, nodeVisitor: (node: FilterExpression) => boolean): void {
        if(!node) return;

        // We execute our visitor function and keep recursing this branch of the tree or not depending on the visitor's result
        let shouldKeepVisiting: boolean = nodeVisitor(node);

        if(shouldKeepVisiting) {
            for(let value of node.values) {
                if(value instanceof FilterExpression) {
                    FilterExpression.traversePreOrder(<FilterExpression>value, nodeVisitor);
                }
            }
        }
    }

    public static traversePostOrder<TResult>(node: FilterExpression, nodeVisitor: (node: FilterExpression, childrenResults: TResult[]) => TResult): TResult {

        let results = new Array<TResult>();

        for(let value of node.values) {
            if(value instanceof FilterExpression) {
                results.push(FilterExpression.traversePostOrder<TResult>(<FilterExpression>value, nodeVisitor));
            }
        }

        return nodeVisitor(node, results);
    }

    public getOperator(): Operator {
        return this.operator;
    }

    public getValues(): any[] {
        return this.values;
    }
}