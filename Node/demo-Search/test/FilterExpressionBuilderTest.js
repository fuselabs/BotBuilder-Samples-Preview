
var botsearch = require('../');
var assert = require('assert');
var searchSchema = require("./RealEstate");

describe('FilterExpressionBuilder', function() {
    it('keywords are extracted successfuly given a set of entities from a luis result', function(done) {
        
        //Code the response from LUIS from the query '2+ bed house with a fireplace and stainless steel'

        let originalText = '2+ bed house with a fireplace and stainless steel';
        let entities = getEntities();
        let canonicalizers = botsearch.CanonicalizerBuilder.build(searchSchema);
        let filterBuilder = new botsearch.FilterExpressionBuilder(canonicalizers, searchSchema);

        let filter = filterBuilder.build(entities, originalText);

        done();
    });

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