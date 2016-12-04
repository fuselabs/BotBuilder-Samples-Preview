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

export class Controls {

    public static introButtons(session: Session): IIsCardAction[] {
        return [
            Controls.browse(session),
            Controls.quit(session)
        ]
    }

    public static refineButtons(session: Session): IIsCardAction[] {
        return [
            Controls.browse(session),
            Controls.nextPage(session),
            Controls.list(session),
            Controls.finished(session),
            Controls.quit(session),
            Controls.startOver(session)
        ]
    }

    public static browse(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'browse')
    }

    public static nextPage(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'next_page')
    }

    public static list(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'list')
    }

    public static add(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'add')
    }

    public static remove(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'remove')
    }

    public static quit(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'quit')
    }

    public static finished(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'finished')
    }

    public static startOver(session: Session): IIsCardAction {
        return Controls.localizedButton(session, 'start_over')
    }

    public static localizedButton(session: Session, textKey: string) {
        let locale: string = session.preferredLocale();
        let text: string = session.localizer.gettext(locale, textKey);

        return CardAction.imBack(session, text, text);
    }
}