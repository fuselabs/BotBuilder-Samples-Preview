
var botsearch = require('../');
var assert = require('assert');

describe('SearchQueryBuilder', function() {
    
    it('General test with full text and filters, no facets', function(done) {
        
        //Build a filter expression for 2+beds 2 bath
        let leftExpressionDescription = '2+ beds';
        let rightExpressionDescription = '2 bath';
        
        let leftExpression = new botsearch.FilterExpression(leftExpressionDescription, botsearch.Operator.GreaterThanOrEqual, {Name: 'beds'}, 2);
        let rightExpression = new botsearch.FilterExpression(rightExpressionDescription, botsearch.Operator.Equal, {Name: 'bath'}, 2);

        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And);

        //Create a search specification
        let searchSpec = {
            phrases: ['fireplace', 'stainless steel'],
            filter: combinedExpression,
            selection: [],
            skip: 0,
            top: 5,
            facet: ''
        };

        //Build search query
        let searchQueryBuilder = new botsearch.SearchQueryBuilder();
        let searchQuery = searchQueryBuilder.build(searchSpec);

        assert.equal('\'fireplace\' OR \'stainless steel\'', searchQuery.search);
        assert.equal('(beds ge 2) and (bath eq 2)', searchQuery.filter);
        done();
    });
});