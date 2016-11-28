
var botsearch = require('../');
var assert = require('assert');

describe('Canonicalizer', function() {
    
    it('when canonical exists and passed in constructor, should return the correct canonical', function(done) {
        
        let synonyms = [{
            Canonical: 'beds',
            Alternatives: ['bedrooms', 'bed']
        }];
        let canonicalizer = new botsearch.Canonicalizer(synonyms);

        let returnedCanonical = canonicalizer.canonicalize('bedrooms');
        assert.equal(synonyms[0].Canonical, returnedCanonical);
        done();
    });

    it('when canonical does not exist, should return null', function(done) {
        
        let synonyms = [{
            Canonical: 'beds',
            Alternatives: ['rooms', 'bed']
        }];
        let canonicalizer = new botsearch.Canonicalizer(synonyms);

        let returnedCanonical = canonicalizer.canonicalize('bedrooms');
        assert.equal(null, returnedCanonical);
        done();
    });
    
    it('when canonical exists through addCanonical, should return the correct canonical', function(done) {
        
        let synonyms = [{
            Canonical: 'beds',
            Alternatives: ['rooms', 'bed']
        }];
        let otherSynonyms = {
            Canonical: 'beds',
            Alternatives: ['bedrooms']
        };
        let canonicalizer = new botsearch.Canonicalizer(synonyms);
        canonicalizer.addSynonyms(otherSynonyms);

        let returnedCanonical = canonicalizer.canonicalize('bedrooms');
        assert.equal(synonyms[0].Canonical, returnedCanonical);
        done();
    });

    it('when alternative has extra spaces and different case and canonical exists and passed in constructor, should return the correct canonical', function(done) {
        
        let synonyms = [{
            Canonical: 'beds',
            Alternatives: ['bedrOoms ', ' Bed   ']
        }];
        let canonicalizer = new botsearch.Canonicalizer(synonyms);

        let returnedCanonical = canonicalizer.canonicalize(' bedrOoms      ');
        assert.equal('beds', returnedCanonical);
        done();
    });
});