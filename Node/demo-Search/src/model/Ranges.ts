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

import { ISearchSchema, ISearchField } from '../search/ISearchSchema';
import { ComparisonSpecification } from './ComparisonSpecification';
import { IDialogCanonicalizers } from './CanonicalizerBuilder';
import { IEntity } from 'botbuilder';

export interface IRange {
    property: ISearchField,
    lower: any,
    upper: any,
    includeLower: boolean,
    includeUpper: boolean,
    description: string
}

export class Ranges {

    public constructor(private schema: ISearchSchema, private canonicalizers: IDialogCanonicalizers) {}

    public resolve(entityComparison: ComparisonSpecification, originalText: string, defaultProperty?: string): IRange {
        let range: IRange = {
            description: null,
            includeLower: false,
            includeUpper: false,
            lower: null,
            upper: null,
            property: null
        };

        let lowerLimit = this.parseRangeLimit(entityComparison.getLower());
        let upperLimit = this.parseRangeLimit(entityComparison.getUpper()); 

        if(lowerLimit && upperLimit && lowerLimit.isCurrency != upperLimit.isCurrency) {
            throw Error('Range does not match in entity unit for upper and lower bound. One of the ranges is currency and the other is not.');
        }

        let isCurrency: boolean = lowerLimit ? lowerLimit.isCurrency : (upperLimit ? upperLimit.isCurrency : false);

        let lower: any = lowerLimit ? lowerLimit.value : Number.NEGATIVE_INFINITY;
        let upper: any = upperLimit ? upperLimit.value : Number.POSITIVE_INFINITY;
        
        let propertyName: string;

        if(!entityComparison.getProperty()) {
            if(isCurrency) {
                propertyName = this.schema.DefaultCurrencyProperty;
            } 
            else {
                propertyName = defaultProperty;
            }
        }
        else {
            propertyName = this.canonicalizers.fieldCanonicalizer.canonicalize(entityComparison.getProperty().entity);
        }

        if(propertyName) {
            range.property = this.schema.Fields[propertyName];

            if(!lower) {
                lower = originalText.substr(entityComparison.getLower().startIndex, entityComparison.getLower().endIndex - entityComparison.getLower().startIndex + 1);
            }
            if(!upper) {
                upper = originalText.substr(entityComparison.getUpper().startIndex, entityComparison.getUpper().endIndex - entityComparison.getUpper().startIndex + 1);
            }
            if(!entityComparison.getOperator()) {
                //This is the case where we have just naked values
                range.includeLower = true;
                range.includeUpper = true;
                upper = lower;
            }
            else {
                switch(entityComparison.getOperator().entity) {
                    case '>=':
                    case '+':
                    case 'greater than or equal':
                    case 'at least':
                    case 'no less than':
                        range.includeLower = true;
                        range.includeUpper = true;
                        upper = Number.POSITIVE_INFINITY;
                        break;
                    case '>':
                    case 'greater than':
                    case 'more than':
                        range.includeLower = false;
                        range.includeUpper = true;
                        upper = Number.POSITIVE_INFINITY;
                        break;
                    case '-':
                    case 'between':
                    case 'and':
                    case 'or':
                        range.includeLower = true;
                        range.includeUpper = true;
                        break;
                    case '<=':
                    case 'no more than':
                    case 'less than or equal':
                        range.includeLower = true;
                        range.includeUpper = true;
                        upper = lower;
                        lower = Number.NEGATIVE_INFINITY;
                        break;
                    case '<':
                    case 'less than':
                        range.includeLower = true;
                        range.includeUpper = true;
                        upper = lower;
                        lower = Number.NEGATIVE_INFINITY;
                        break;
                    case 'any':
                    case 'any number of':
                        upper = Number.POSITIVE_INFINITY;
                        lower = Number.NEGATIVE_INFINITY;
                        break;
                    default:
                        throw new Error('Operator not supported ' + entityComparison.getOperator().entity);
                }
            }
            range.lower = lower;
            range.upper = upper;
            let entity = entityComparison.getEntity();
            range.description = entity ? entity.entity : null;
        }

        return range;
    }

    private parseRangeLimit(entity: IEntity): ICurrencyNumber {
        let isCurrency: boolean = false;
        let multiplier: number = 1;
        if(!entity) return null;

        let entityString = entity.entity;

        entityString = entityString.trim();
        if(entityString.charAt(0) == '$') {
            isCurrency = true;
            entityString = entityString.substring(1);
        }
        if(entityString.indexOf('k') == entityString.length - 1) {
            multiplier = 1000;
            entityString = entityString.substring(0, entityString.length - 1);
        }

        let numberString = entityString.replace(',', '').replace(' ', '');
        let parsedNumber: number = +numberString;

        if(parsedNumber) {
            parsedNumber *= multiplier;
        } else {
            parsedNumber = null;
        }

        let response: ICurrencyNumber = {
            isCurrency: isCurrency,
            value: parsedNumber
        };

        return response;
    }
}

export interface ICurrencyNumber {
    value: number;
    isCurrency: boolean;
}