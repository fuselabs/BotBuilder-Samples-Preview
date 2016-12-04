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

import { IEntity, IRecognizeContext } from 'botbuilder';
import { IWordAnalyzer, LocalizedWordAnalyzer } from './LocalizedWordAnalyzer';
import { StringBuilder } from '../tools/StringBuilder';

export class Keywords {

    private readonly wordAnalyzer: IWordAnalyzer;

    public constructor(context?: IRecognizeContext, wordAnalyzer?: IWordAnalyzer) {
        if(wordAnalyzer) {
            this.wordAnalyzer = wordAnalyzer;
        } else {
            this.wordAnalyzer = new LocalizedWordAnalyzer(context);
        }
    }

    public phrases(entities: IEntity[], originalText: string): string[] {
        let ranges: IStringRange[] = [];
        ranges.push({start: 0, end: originalText.length });

        for(let entity of entities) {
            let i: number = 0;

            while(i < ranges.length) {
                let range = ranges[i];
                if(range.start > entity.endIndex){
                    break;
                }

                if(range.start == entity.startIndex) {
                    if(range.end <= entity.endIndex) {
                        // Completely contained
                        ranges.splice(i, 1); // remove index i
                    }
                    else {
                        // Remove from start
                        ranges.splice(i, 1); //remove at index i
                        ranges.splice(i, 0, {start: entity.endIndex + 1, end: range.end}); //insert at index i
                        i++; 
                    }
                }
                else if(range.end == entity.endIndex) {
                    ranges.splice(i, 1);
                    ranges.splice(i, 0, {start: range.start, end: entity.startIndex}); //insert at index i
                    i++;
                }
                else if(range.start < entity.startIndex && range.end > entity.endIndex) {
                    // Split
                    ranges.splice(i, 1);
                    ranges.splice(i, 0, {start: range.start, end: entity.startIndex});
                    ranges.splice(++i, 0, {start: entity.endIndex + 1, end: range.end});
                    i++;
                }
                else if(range.start > entity.startIndex && range.end < entity.endIndex) {
                    // Completely contained
                    ranges.splice(i, 1); // remove index i
                }
                else {
                    i++;
                }
            }
        }

        let substrings: string[] = [];
        for(let range of ranges) {
            let str = originalText.substr(range.start, range.end - range.start);
            let newSubstrings = this.extractPhrases(str);

            if(newSubstrings.length > 0) {
                substrings = substrings.concat(newSubstrings);
            }
        }

        //Rudimentary way of removing duplicates
        return substrings.filter((value: string, index: number, array: string[]): any => {
             return array.lastIndexOf(value) == index;
         });
    }

    private extractPhrases(str: string): string[] {
        let stringBuilder = new StringBuilder();
        let phrases: string[] = [];

        str = str.trim();

        if(str.length == 0 || str == '') {
            return phrases;
        }

        let words = str.split(' ');

        for(let word of words) {
            word = word.trim();

            if(word.length > 0) {
                if(this.wordAnalyzer.isNoiseWord(word)) {
                    if (!stringBuilder.empty()) {
                        phrases.push(stringBuilder.toString());
                        stringBuilder = new StringBuilder();
                    }
                }
                else if (this.wordAnalyzer.isPunctuation(word.charAt(word.length -1))) {
                    let lastPunctuation = 0;

                    if(stringBuilder.empty()) {
                        continue;
                    }

                    for(let i: number = 0; i < word.length; i++) {
                        if(this.wordAnalyzer.isPunctuation(word.charAt(i))) {
                            stringBuilder.append(word.substring(0, lastPunctuation));
                            phrases.push(stringBuilder.toString());
                            lastPunctuation = i;
                            stringBuilder = new StringBuilder();
                        }
                    }
                }
                else {
                    if(!stringBuilder.empty()) {
                        stringBuilder.append(' ');
                    }
                    stringBuilder.append(word);
                }
            }
        }
        if(!stringBuilder.empty()) {
            let phrase: string = stringBuilder.toString();

            if(phrase.length > 0) {
                phrases.push(phrase);
            }
            
        }
        return phrases;
    }
}

export interface IStringRange {
    start: number;
    end: number;
}