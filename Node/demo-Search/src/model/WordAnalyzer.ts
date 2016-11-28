

export interface IWordAnalyzer {
    isNoiseWord(word: string): boolean;
    isPunctuation(word: string): boolean;
}

export class EnglishWordAnalyzer {

    private readonly stopWords: string[] = ["","a","able","about","across","after","all","almost","also","am","among","an","and","any","are","as","at","be","because","been","but","by","can","cannot","could","dear","did","do","does","either","else","ever","every","for","from","get","got","had","has","have","he","her","hers","him","his","how","however","i","if","in","into","is","it","its","just","least","let","like","likely","may","me","might","most","must","my","neither","no","nor","not","of","off","often","on","only","or","other","our","own","rather","said","say","says","she","should","since","so","some","than","that","the","their","them","then","there","these","they","this","tis","to","too","twas","us","wants","was","we","were","what","when","where","which","while","who","whom","why","will","with","would","yet","you","your"];
            
    private readonly punctuationChars: string[] = ['!', '"', '#', '%', '&', '\'', '(', ')', '*', ',', '-', '.', '/', ':', ';', '?', '@', '[', '\\', ']', '_', '{', '}'];

    public constructor(){}

    public isNoiseWord(word: string): boolean {
        return this.stopWords.indexOf(word) != -1;
    }

    public isPunctuation(word: string): boolean {
        return this.punctuationChars.indexOf(word) != -1;
    }
}