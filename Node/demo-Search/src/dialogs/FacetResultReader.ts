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

import { Session, IIsCardAction, CardAction } from 'botbuilder';
import { IFacet } from '../search/ISearchClient';
import { ISearchField } from '../search/ISearchSchema';
import { IDialogCanonicalizers } from '../model/CanonicalizerBuilder';
import { PreferredFilters } from '../search/SearchConstants';

export class FacetResultReader {

    public constructor(private canonicalizers: IDialogCanonicalizers) { }

    public toButtons(session: Session, facets: IFacet[], field: ISearchField, fieldDescription: string): IIsCardAction[]  {

        let buttons: IIsCardAction[] = [];

        // Canonicalize facets 
        let canonicalizedFacets: IFacet[] = [];

        for(let entry of facets) {
            let description: string = this.description(entry);

            canonicalizedFacets.push({ value: description, count: entry.count });
        }
        // Sort facets by description ascending
        canonicalizedFacets = canonicalizedFacets.sort((a, b) => a.value > b.value ? 1 : (a.value < b.value ? -1 : 0));

        // Generate buttons depending on the filter preference for the field being faceted
        if(field.FilterPreference == PreferredFilters.Facet) {
            for(let choice of canonicalizedFacets) {
                
                buttons.push(
                    CardAction.imBack(session, choice.value /* + (searchFieldSynonym ? ' ' + searchFieldSynonym : '')*/, choice.value + ' (' + choice.count + ')')
                );
            }
        }
        else if(field.FilterPreference == PreferredFilters.MinValue) {
            let total: number = 0;
            
            for(let choice of canonicalizedFacets) {
                total += choice.count;
            }

            for(let choice of canonicalizedFacets) {
                buttons.push(
                    CardAction.imBack(session, choice.value + '+ ' + fieldDescription, choice.value + '+ (' + total + ')')
                );
                total -= choice.count;
            }
        }
        else if(field.FilterPreference == PreferredFilters.MaxValue) {
            let total: number = 0;
            
            for(let choice of canonicalizedFacets) {

                total += choice.count;
                buttons.push(
                    CardAction.imBack(session,'<= ' + choice.value + ' ' + fieldDescription, '<= ' + choice.value + ' (' + total + ')')
                );
            }
        }

        if(buttons.length > 0) {
            buttons.push(
                CardAction.imBack(session, 'Any ' + fieldDescription, 'Any ' + fieldDescription)
            );
        }
        return buttons;
    }

    public description(facet: IFacet): string {
        let description: string = facet.value;
        let canonicalValue = this.canonicalizers.valueCanonicalizers[description];

        if(canonicalValue) {
            description = canonicalValue.description;
        }
        return description;
    }
}