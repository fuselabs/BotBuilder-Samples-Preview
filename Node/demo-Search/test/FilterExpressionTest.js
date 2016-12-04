
var botsearch = require('../');
var assert = require('assert');

describe('FilterExpression', function() {
    
    //ToUserFriendlyString tests

    it('ToUserFriendlyString returns valid string for simple single-node expression', function(done) {
        let expressionDescription = '2+ beds';
        let expression = new botsearch.FilterExpression(expressionDescription, botsearch.Operator.And, 'disregard');
        let userFriendlyString = expression.toUserFriendlyString();

        assert.equal('\"' + expressionDescription + '\"', userFriendlyString);

        done();
    });

    //Combine + ToUserFriendlyString tests

    it('Combine + ToUserFriendlyString returns valid string for simple 3-node expression', function(done) {
        
        let leftExpressionDescription = '2+ beds';
        let rightExpressionDescription = '2 bath';
        
        let leftExpression = new botsearch.FilterExpression(leftExpressionDescription, botsearch.Operator.GreaterThanOrEqual, 'beds', '2');
        let rightExpression = new botsearch.FilterExpression(rightExpressionDescription, botsearch.Operator.Equal, 'bath', '2');

        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And);
        let userFriendlyString = combinedExpression.toUserFriendlyString();

        assert.equal('\"' + leftExpressionDescription + '\" \"' + rightExpressionDescription + '\"', userFriendlyString);

        done();
    });

    it('Combine + ToUserFriendlyString stops traversing subtrees when a node has a description', function(done) {
        
        let leftExpressionDescription = '2+ beds';
        let rightExpressionDescription = '2 bath';
        let rootDescription = 'subtree root description';

        let leftExpression = new botsearch.FilterExpression(leftExpressionDescription, botsearch.Operator.GreaterThanOrEqual, 'beds', '2');
        let rightExpression = new botsearch.FilterExpression(rightExpressionDescription, botsearch.Operator.Equal, 'bath', '2');

        //Add description to the combination, the subtree should not be traversed then
        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And, rootDescription);
        let userFriendlyString = combinedExpression.toUserFriendlyString();

        assert.equal('\"' + rootDescription + '\"', userFriendlyString);

        done();
    });

    it('Combine should only keep the right subtree if the left is null', function(done) {

        let leftExpression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, 'beds', '2');
        let rightExpression = null;

        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And);

        assert.deepEqual(leftExpression, combinedExpression);

        done();
    });

    it('Combine should only keep the left subtree if the right is null', function(done) {

        let rightExpression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, 'beds', '2');
        let leftExpression = null;

        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And);

        assert.deepEqual(rightExpression, combinedExpression);

        done();
    });

    //RetrieveSearchField tests

    it('RetrieveSearchField retrieves search fields in the expression tree correctly', function(done) {
        
        let searchField1 = createSearchField('name1');
        let searchField2 = createSearchField('name2');
        let searchField3 = createSearchField('name3');

        //Build the following tree
        //     And
        //      |
        //   -----------
        //   |         |
        //  field1    And
        //             |
        //        ------------
        //        |          |
        //      field2     field3


        let field1Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField1, '2');
        let field2Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField2, '3');
        let field3Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField3, '4');

        let fields2And3Expression = botsearch.FilterExpression.combine(field2Expression, field3Expression, botsearch.Operator.And);
        let rootExpression = botsearch.FilterExpression.combine(field1Expression, fields2And3Expression);

        //Verify that the 3 search fields are retrieved
        let searchFields = rootExpression.retrieveSearchFields();

        assert.equal(3, searchFields.length);

        assert.notEqual(-1, searchFields.indexOf(searchField1));
        assert.notEqual(-1, searchFields.indexOf(searchField2));
        assert.notEqual(-1, searchFields.indexOf(searchField3));

        done();
    });

    it('RetrieveSearchField returns an empty array when no search fields in the expression tree', function(done) {
        
        let leftExpressionDescription = '2+ beds';
        let rightExpressionDescription = '2 bath';
        let rootDescription = 'subtree root description';

        let leftExpression = new botsearch.FilterExpression(leftExpressionDescription, botsearch.Operator.GreaterThanOrEqual, 'beds', '2');
        let rightExpression = new botsearch.FilterExpression(rightExpressionDescription, botsearch.Operator.Equal, 'bath', '2');

        let combinedExpression = botsearch.FilterExpression.combine(leftExpression, rightExpression, botsearch.Operator.And, rootDescription);

        let searchFields = combinedExpression.retrieveSearchFields();

        assert.notEqual(null, searchFields);
        assert.notEqual(undefined, searchFields);
        assert.deepEqual([], searchFields);

        done();
    });

    //RemoveSearchField tests

    it('RemoveSearchField removes search field from the expression tree correctly when removing from the largest subtree', function(done) {
        
        let searchField1 = createSearchField('name1');
        let searchField2 = createSearchField('name2');
        let searchField3 = createSearchField('name3');

        //Build the following tree
        //     And
        //      |
        //   -----------
        //   |         |
        //  field1    And
        //             |
        //        ------------
        //        |          |
        //      field2     field3

        let field1Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField1, '2');
        let field2Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField2, '3');
        let field3Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField3, '4');

        let fields2And3Expression = botsearch.FilterExpression.combine(field2Expression, field3Expression, botsearch.Operator.And);
        let rootExpression = botsearch.FilterExpression.combine(field1Expression, fields2And3Expression);

        //Build the following tree
        //     And
        //      |
        //   -----------
        //   |         |
        //  field1    And
        //             |
        //        ------------
        //        |          |
        //      field1     field3


        //Remove field 2
        let resultingExpression = rootExpression.removeSearchField(searchField2);

        //Verify that retrieving the search fields works correctly after removing field2's node
        let remainingSearchFields = resultingExpression.retrieveSearchFields();
        assert.equal(2, remainingSearchFields.length);

        assert.notEqual(-1, remainingSearchFields.indexOf(searchField1));
        assert.notEqual(-1, remainingSearchFields.indexOf(searchField3));
        assert.equal(-1, remainingSearchFields.indexOf(searchField2));

        done();
    });

    const createSearchField = function(name){
        return {
            Name: name,
            Type: 'string',
            IsFacetable: false,
            IsFilterable: true,
            IsSerchable: true,
            IsRetrievable: true,
            IsSortable: true,
            FilterPreference: ''
        }
    };

    it('RemoveSearchField removes search field from the expression tree correctly when removing from the smallest subtree, leaving half tree empty', function(done) {

        let searchField1 = createSearchField('name1');
        let searchField2 = createSearchField('name2');
        let searchField3 = createSearchField('name3');

        //Build the following tree
        //     And
        //      |
        //   -----------
        //   |         |
        //  field1    And
        //             |
        //        ------------
        //        |          |
        //      field2     field3


        let field1Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField1, '2');
        let field2Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField2, '3');
        let field3Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField3, '4');

        let fields2And3Expression = botsearch.FilterExpression.combine(field2Expression, field3Expression, botsearch.Operator.And);
        let rootExpression = botsearch.FilterExpression.combine(field1Expression, fields2And3Expression);

        //Remove field1 and verify that we get the following tree
        //     And
        //      |
        //   -----------
        //   |         |
        //  field2    field3

        //Remove field 1
        let resultingExpression = rootExpression.removeSearchField(searchField1);

        //Verify that retrieving the search fields works correctly after removing field2's node
        let remainingSearchFields = resultingExpression.retrieveSearchFields();
        assert.equal(2, remainingSearchFields.length);

        assert.notEqual(-1, remainingSearchFields.indexOf(searchField2));
        assert.notEqual(-1, remainingSearchFields.indexOf(searchField3));
        assert.equal(-1, remainingSearchFields.indexOf(searchField1));

        done();
    });

    //Remove expression tests
    it('remove should remove the entire subtree related to a search expression', function(done) {

        let searchField1 = createSearchField('name1');
        let searchField2 = createSearchField('name2');
        let searchField3 = createSearchField('name3');

        //Build the following tree
        //     And (1)
        //      |
        //   -----------
        //   |         |
        //  field1    And (2)
        //             |
        //        ------------
        //        |          |
        //      field2     field3

        let field1Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField1, '2');
        let field2Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField2, '3');
        let field3Expression = new botsearch.FilterExpression(null, botsearch.Operator.GreaterThanOrEqual, searchField3, '4');

        let fields2And3Expression = botsearch.FilterExpression.combine(field2Expression, field3Expression, botsearch.Operator.And);
        let rootExpression = botsearch.FilterExpression.combine(field1Expression, fields2And3Expression);

        //Remove the And(2) expression and verify that the result has field1 only as search field
        let resultingExpression = rootExpression.remove(fields2And3Expression);

        //Verify that retrieving the search fields works correctly after removing And (2) node from the tree 'diagram' above
        let remainingSearchFields = resultingExpression.retrieveSearchFields();
        assert.equal(1, remainingSearchFields.length);

        assert.equal(-1, remainingSearchFields.indexOf(searchField2));
        assert.equal(-1, remainingSearchFields.indexOf(searchField3));
        assert.notEqual(-1, remainingSearchFields.indexOf(searchField1));

        done();
    });
});