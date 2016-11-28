
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
                                    description: synonym.Canonical,
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