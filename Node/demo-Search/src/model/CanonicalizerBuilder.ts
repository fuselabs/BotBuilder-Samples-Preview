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

import { Canonicalizer, ICanonicalValue } from './Canonicalizer';
import { Normalizer } from './Normalizer';
import { ISearchSchema } from '../search/ISearchSchema';

export interface IDialogCanonicalizers {
    fieldCanonicalizer: Canonicalizer;
    valueCanonicalizers: {[value: string]: ICanonicalValue};
}

export class CanonicalizerBuilder {
    public static build(searchSchema: ISearchSchema): IDialogCanonicalizers {

        let canonicalizers: IDialogCanonicalizers = {
            fieldCanonicalizer: new Canonicalizer(),
            valueCanonicalizers: {}
        }

        // Load name synonyms and value synonyms from the search schema in the canonicalizers.
        if(searchSchema) {
            for(let fieldKey in searchSchema.Fields) {
                let field = searchSchema.Fields[fieldKey];

                if(field.NameSynonyms) {
                    canonicalizers.fieldCanonicalizer.addSynonyms(field.NameSynonyms);
                }
                
                if(field.ValueSynonyms) {
                    for(let synonym of field.ValueSynonyms) {
                        if(synonym.Alternatives) {
                            for(let alternative of synonym.Alternatives) {

                                let canonicalValue: ICanonicalValue = {
                                    description: synonym.Alternatives[0],
                                    field: field,
                                    value: synonym.Canonical,
                                }
                                canonicalizers.valueCanonicalizers[Normalizer.normalize(alternative)] = canonicalValue;
                            }
                        }
                    }
                }
            }
        }
        return canonicalizers;
    }
}