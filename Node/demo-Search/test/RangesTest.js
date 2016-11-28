
var botsearch = require('../');
var assert = require('assert');
var searchSchema = require("./RealEstate");

describe('Ranges', function() {
    it('ranges are calculated correctly for a query with multiple keywords and comparisons', function(done) {
        
        //Code the response from LUIS from the query 2+ bed house with a fireplace and stainless steel

        let originalText = '2+ bed house with a fireplace and stainless steel';

        let comparison = new botsearch.ComparisonSpecification(getComparisonEntity());
        comparison.addEntity(getAttributeEntity());
        comparison.addEntity(getPropertyEntity());
        comparison.addEntity(getValueEntity());
        comparison.addEntity(getOperatorEntity());

        let canonicalizers = botsearch.CanonicalizerBuilder.build(searchSchema);
        let rangesResolver = new botsearch.Ranges(searchSchema, canonicalizers);
        let range = rangesResolver.resolve(comparison, originalText);

        assert.equal('2 + bed', range.description);
        assert.equal(true, range.includeLower);
        assert.equal(true, range.includeUpper);
        assert.equal(2, range.lower);
        assert.equal(Number.POSITIVE_INFINITY, range.upper);
        assert.equal('beds', range.property.Name);

        done();
    });

    const getPropertyEntity = function() {
        return {
            endIndex: 5,
            startIndex: 3,
            entity: 'bed',
            type: 'Property'
        };
    };

    const getComparisonEntity = function() {
        return {
            endIndex: 5,
            startIndex: 0,
            entity: '2 + bed',
            type: 'Comparison'
        };
    };

    const getOperatorEntity = function() {
        return {
            endIndex: 1,
            startIndex: 1,
            entity: '+',
            type: 'Operator'
        };
    };

    const getAttributeEntity = function() {
        return {
            endIndex: 11,
            startIndex: 7,
            entity: 'house',
            type: 'Attribute'
        };
    };

    const getValueEntity = function() {
        return {
            endIndex: 0,
            startIndex: 0,
            entity: '2',
            type: 'Value'
        };
    };
});