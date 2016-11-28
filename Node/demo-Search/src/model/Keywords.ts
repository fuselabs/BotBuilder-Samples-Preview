
import { IEntity } from 'botbuilder';
import { IWordAnalyzer, EnglishWordAnalyzer } from './WordAnalyzer';

export class Keywords {

    public static phrases(entities: IEntity[], originalText: string, wordAnalyzer: IWordAnalyzer = new EnglishWordAnalyzer()): string[] {
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
            substrings = substrings.concat(Keywords.extractPhrases(str, wordAnalyzer));
        }

        //Rudimentary way of removing duplicates
        return substrings.filter((value: string, index: number, array: string[]): any => {
             return array.lastIndexOf(value) == index;
         });
    }

    private static extractPhrases(str: string, wordAnalyzer: IWordAnalyzer = new EnglishWordAnalyzer()): string[] {
        let stringBuilder: string[] = [];
        let phrases: string[] = [];

        let words = str.split(' ');

        for(let word of words) {
            word = word.trim();

            if(word.length > 0) {
                if(wordAnalyzer.isNoiseWord(word)) {
                    if (stringBuilder.length > 0) {
                        phrases.push(stringBuilder.join(''));
                        stringBuilder = [];
                    }
                }
                else if (wordAnalyzer.isPunctuation(word.charAt(word.length -1))) {
                    let lastPunctuation = 0;
                    for(let i: number = 0; i < word.length; i++) {
                        if(wordAnalyzer.isPunctuation(word.charAt(i))) {
                            //TODO: Check c# code for this with Chris
                            stringBuilder.push(word.substring(lastPunctuation == 0 ? lastPunctuation: lastPunctuation + 1 , i - 1));
                            phrases.push(stringBuilder.join(''));
                            lastPunctuation = i;
                            stringBuilder = [];
                        }
                    }
                }
                else {
                    if(stringBuilder.length > 0) {
                        stringBuilder.push(' ');
                    }
                    stringBuilder.push(word);
                }
            }
        }
        if(stringBuilder.length > 0) {
            phrases.push(stringBuilder.join(''));
        }
        return phrases;
    }
}

export interface IStringRange {
    start: number;
    end: number;
}