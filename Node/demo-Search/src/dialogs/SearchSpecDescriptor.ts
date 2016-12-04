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

import { ISearchSpecification } from './SearchSpecificationManager';
import { StringBuilder } from '../tools/StringBuilder';
import { Session } from 'botbuilder';
import { sprintf } from 'sprintf-js';

export var DialogStatus = {
    filter: 'Filter: %s',
    keywords: 'Keywords: %s',
    sort: 'Sort: %s',
    page: 'Page: %d',
    count: 'Total results: %d',
    selected: 'Kept %d results so far.',
    ascending: 'Ascending',
    descending: 'descending'
};

export class SearchSpecDescriptor {

    public static describeSearch(session: Session, searchSpec: ISearchSpecification): string {

        let searchDescription = new StringBuilder();

        // Selected
        if(searchSpec.selection && searchSpec.selection.length > 0) {
            searchDescription.appendLine(sprintf(session.localizer.gettext(session.preferredLocale(), 'selected_status'), searchSpec.selection.length));
        }

        // Filter description
        if(searchSpec.filter) {    
            searchDescription.appendLine(sprintf(session.localizer.gettext(session.preferredLocale(), 'filter_status') + '<br>', searchSpec.filter.toUserFriendlyString()));
        }
        
        // Phrases description, build a string with the quoted phrases separated by spaces
        if(searchSpec.phrases && searchSpec.phrases.length > 0) {
            const spacePrefix = ' ';
            const emptyPrefix = '';
            let prefix = emptyPrefix;

            let phrases = new StringBuilder();

            for(let phrase of searchSpec.phrases) {
                phrases.append(prefix);
                phrases.appendDoubleQuoted(phrase);
                if(prefix == emptyPrefix) {
                    prefix = spacePrefix;
                }
            }

            searchDescription.appendLine(sprintf(session.localizer.gettext(session.preferredLocale(), 'keywords_status'), phrases.toString()));
        }
        
        if(searchSpec.sort && searchSpec.sort.length > 0) {
            const spacePrefix = ' ';
            const emptyPrefix = '';
            let prefix = emptyPrefix;

            let sorts = new StringBuilder();

            for(let sortKey of searchSpec.sort) {
                sorts.append(prefix);
                sorts.appendDoubleQuoted(sortKey.field + ' ' + sortKey.sortDirection);
                if(prefix == emptyPrefix) {
                    prefix = spacePrefix;
                }
            }

            searchDescription.appendLine(sprintf(session.localizer.gettext(session.preferredLocale(), 'sort_status'), sorts.toString()));
        }

        searchDescription.appendLine(sprintf(session.localizer.gettext(session.preferredLocale(), 'page_status'), searchSpec.pageNumber));

        return searchDescription.toString();
    }
}