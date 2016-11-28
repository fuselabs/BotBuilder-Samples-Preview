
import { IEntity } from 'botbuilder';
import { EntityTypes } from './EntityTypes';

export class ComparisonSpecification {

    private lower: IEntity;
    private upper: IEntity;
    private operator: IEntity;
    private property: IEntity;

    public constructor(private entity: IEntity) {}

    public addEntity(entity: IEntity): void {
        if(!entity) return;
        
        if(entity.type != EntityTypes.Comparison && entity.startIndex >= this.entity.startIndex && entity.endIndex <= this.entity.endIndex) {
            switch(entity.type) {
                case EntityTypes.Currency: this.addNumber(entity); break;
                case EntityTypes.Value: this.addNumber(entity); break;
                case EntityTypes.Dimension: this.addNumber(entity); break;
                case EntityTypes.Operator: this.operator = entity; break;
                case EntityTypes.Property: this.property = entity; break;
            }
        }
    }

    public getLower(): IEntity { return this.lower; }
    public getUpper(): IEntity { return this.upper; }
    public getOperator(): IEntity { return this.operator; }
    public getProperty(): IEntity { return this.property; }
    public getEntity(): IEntity { return this.entity; }


    private addNumber(entity: IEntity): void {
        if(!this.lower) {
            this.lower = entity;
        } else if(entity.startIndex < this.lower.startIndex) {
            this.upper = this.lower;
            this.lower = entity;
        } else {
            this.upper = entity;
        }
    }
}