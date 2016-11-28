
import { ISynonyms } from '../search/ISearchClient';
import { ISearchSchema, ISearchField } from '../search/ISearchSchema';
import { Normalizer } from './Normalizer';

export interface ICanonicalValue {
    field: ISearchField;
    value: string;
    description: string;
}

export class Canonicalizer {
    
    //Simple map of strings to synonyms
    private map: { [s: string]: ISynonyms; } = {}; 

    public constructor(synonymsCollection?: ISynonyms[]) {    
        if(synonymsCollection) {
            for(let synonyms of synonymsCollection) {
                this.addSynonyms(synonyms);
            }
        }
    }

    public canonicalize(source: string): string {

        let normalized = Normalizer.normalize(source);

        if(source) {
            let synonyms: ISynonyms = this.map[normalized];
            if(synonyms) {
                return synonyms.Canonical;
            }
        }
        return null;
    }

    public addSynonyms(synonyms: ISynonyms): void {
        if(synonyms){
            for(let alternative of synonyms.Alternatives) {
                let key = Normalizer.normalize(alternative);

                if(!this.map[key]) {
                    this.map[key] = synonyms;
                }
            }
        }
    }
}