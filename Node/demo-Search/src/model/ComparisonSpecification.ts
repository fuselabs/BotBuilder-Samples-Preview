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