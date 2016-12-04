
var botsearch = require('../');
var assert = require('assert');
var util = require('util');
var searchSchema = require("./RealEstate");

describe('AzureSearchClient', function() {
    
    const hitMapper = function(obj) {
        return {
            key: obj['listingId'],
            description: obj['description'],
            title: util.format('%d bedroom, %d bath in %s, %s, $%d', obj['beds'], obj['baths'], obj['city'], obj['region'].toUpperCase(), obj['price']),
            thumbnailUrl: obj['thumbnail']
        };
    }

    it('General test with full text and filters, no facets', function(done) {

        //Build a filter expression for 2+beds 2 bath
        let leftExpressionDescription = '2+ beds';
        let rightExpressionDescription = '2 bath';
        
        let leftExpression = new botsearch.FilterExpression(leftExpressionDescription, botsearch.Operator.GreaterThanOrEqual, {Name: 'beds'}, 2);
        let rightExpression = new botsearch.FilterExpression(rightExpressionDescription, botsearch.Operator.Equal, {Name: 'baths'}, 2);

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

        //Create Azure Search client
        const serviceUrl = 'https://realestate.search.windows.net';
        const serviceKey = '82BCF03D2FC9AC7F4E9D7DE1DF3618A5';
        const indexName = 'listings'

        let searchClient = new botsearch.AzureSearchClient(serviceUrl, serviceKey, indexName, searchSchema, hitMapper);        

        let searchResult = searchClient.search(searchQuery, function(error, result){
            if(error) {
                done(error);
            } else {
                assert.equal(5, result.hits.length);
                done();
            }
        });
    });
});