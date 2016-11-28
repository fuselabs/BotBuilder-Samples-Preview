
var botsearch = require('../');
var assert = require('assert');

describe('EnglishWordAnalyzer', function() {
    it('isPunctuation should return true for punctiation and false for letters, numbers and non-punctuation symbols', function(done) {

        let language = new botsearch.EnglishWordAnalyzer();
        assert.equal(true, language.isPunctuation('!'));
        assert.equal(false, language.isPunctuation('home'));
        assert.equal(false, language.isPunctuation('~'));
        done();
    });

    it('isPunctuation should return true for punctiation and false for letters, numbers and non-punctuation symbols', function(done) {

        let language = new botsearch.EnglishWordAnalyzer();
        assert.equal(true, language.isNoiseWord('if'));
        assert.equal(false, language.isNoiseWord('home'));
        done();
    });
});