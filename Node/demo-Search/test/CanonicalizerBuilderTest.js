
var botsearch = require('../');
var assert = require('assert');
var searchSchema = require("./RealEstate");

describe('CanonicalizerBuilder', function() {
    it('Canonicalizers generated from the real state json search schema should correctly load value and field canonicalizers', function(done) {
        let canonicalizers = botsearch.CanonicalizerBuilder.build(searchSchema);

        let listingCanonical = canonicalizers.fieldCanonicalizer.canonicalize('listing Id');
        assert.equal('listingId', listingCanonical);

        let virginiaCanonical = canonicalizers.valueCanonicalizers['virginia'];
        assert.equal('va', virginiaCanonical.description);

        done();
    });
});