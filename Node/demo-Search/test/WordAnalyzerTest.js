
var botsearch = require('../');
var assert = require('assert');

describe('WordAnalyzer', function() {
    it('isPunctuation should return true for punctiation and false for letters, numbers and non-punctuation symbols', function(done) {

        let language = new botsearch.LocalizedWordAnalyzer(getDummyRecognizeContext());
        assert.equal(true, language.isPunctuation('!'));
        assert.equal(false, language.isPunctuation('home'));
        assert.equal(false, language.isPunctuation('~'));
        done();
    });

    it('isPunctuation should return true for punctiation and false for letters, numbers and non-punctuation symbols', function(done) {

        let language = new botsearch.LocalizedWordAnalyzer(getDummyRecognizeContext());
        assert.equal(true, language.isNoiseWord('if'));
        assert.equal(false, language.isNoiseWord('home'));
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
});