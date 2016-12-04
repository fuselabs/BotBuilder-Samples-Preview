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

import { Session, Message, IIsCardAction, Keyboard, AttachmentLayout, ThumbnailCard, CardImage, CardAction } from 'botbuilder';
import { ISearchSpecification } from './SearchSpecificationManager';
import { ISearchHit } from '../search/ISearchClient';
import { SearchSpecDescriptor } from './SearchSpecDescriptor';
import { sprintf } from 'sprintf-js';

export class DialogAction {
    
    public static sendMessage(session: Session, textKey: string, ...params:any[]) {
        let message = new Message(session)
            .text(sprintf(session.localizer.gettext(session.preferredLocale(), textKey), params));
        session.send(message);
    }

    public static showResults(session: Session, hits: ISearchHit[], searchSpec: ISearchSpecification, showRemove: boolean = false) {
        let message = new Message(session)
            .text(SearchSpecDescriptor.describeSearch(session, searchSpec))
            .attachmentLayout(AttachmentLayout.carousel);

        hits.forEach((hit: ISearchHit) => {
             message.addAttachment(
                new ThumbnailCard(session)
                    .title(hit.title)
                    .text(hit.description)
                    .images([
                        CardImage.create(session, hit.thumbnailUrl)
                    ])
                    .buttons([
                        showRemove 
                        ? CardAction.postBack(session, sprintf(session.localizer.gettext(session.preferredLocale(), 'remove_format'), hit.key), 
                            session.localizer.gettext(session.preferredLocale(), 'remove'))
                        : CardAction.postBack(session, sprintf(session.localizer.gettext(session.preferredLocale(), 'add_format'), hit.key), 
                            session.localizer.gettext(session.preferredLocale(), 'add'))
                    ])
             );
        });

        session.send(message);
    }

    public static sendKeyboard(session: Session, buttons: IIsCardAction[], titleKey: string, ...params: any[]) {
        let text: string = sprintf(session.localizer.gettext(session.preferredLocale(), titleKey), params);
        let message = new Message(session)
            .text(text)
            .addAttachment(
                new Keyboard(session)
                .buttons(buttons)
            );
        session.send(message);
    }
}