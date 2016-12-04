
var botsearch = require('../');
var assert = require('assert');

describe('Keywords', function() {
    it('keywords are extracted successfuly given a set of entities from a luis result', function(done) {
        
        //Code the response from LUIS from the query 2+ bed house with a fireplace and stainless steel

        let originalText = '2+ bed house with a fireplace and stainless steel';
        let entities = getEntities();

        let keywords = new botsearch.Keywords(getDummyRecognizeContext());
        let substrings = keywords.phrases(entities, originalText);

        assert.equal(2, substrings.length);
        assert.notEqual(-1, substrings.indexOf('fireplace'));
        assert.notEqual(-1, substrings.indexOf('stainless steel'));

        done();
    });

    const getDummyRecognizeContext = function() {
        return {
            preferredLocale: function(){ return 'en'; },
            localizer: {
                gettext: function(locale, msgid, namespace){
                    if(msgid == 'wordAnalyzerStopWords') {
                        return "|a|able|about|across|after|all|almost|also|am|among|an|and|any|are|as|at|be|because|been|but|by|can|cannot|could|dear|did|do|does|either|else|ever|every|for|from|get|got|had|has|have|he|her|hers|him|his|how|however|i|if|in|into|is|it|its|just|least|let|like|likely|may|me|might|most|must|my|neither|no|nor|not|of|off|often|on|only|or|other|our|own|rather|said|say|says|she|should|since|so|some|than|that|the|their|them|then|there|these|they|this|tis|to|too|twas|us|wants|was|we|were|what|when|where|which|while|who|whom|why|will|with|would|yet|you|your";
                    } else if (msgid == 'wordAnalyzerPunctuation') {
                        return "!|\"|#|%|&|'|(|)|*|,|-|.|/|:|;|?|@|[|\\|]|_|{|}";
                    } else {
                        throw new Error("Dummy test localizer does not support msgid: " + msgid);
                    }
                }
            }
        };
    }

    const getEntities = function() {
        let bedEntity = {
            endIndex: 5,
            startIndex: 3,
            entity: 'bed',
            type: 'Property'
        };

        let bedComparisonEntity = {
            endIndex: 5,
            startIndex: 0,
            entity: '2 + bed',
            type: 'Comparison'
        };

        let plusOperatorEntity = {
            endIndex: 1,
            startIndex: 1,
            entity: '+',
            type: 'Operator'
        };

        let houseEntity = {
            endIndex: 11,
            startIndex: 7,
            entity: 'house',
            type: 'Attribute'
        };

        let bedValueEntity = {
            endIndex: 0,
            startIndex: 0,
            entity: '2',
            type: 'Value'
        };

        return [bedEntity, bedComparisonEntity, plusOperatorEntity, houseEntity, bedValueEntity];
    }

});