
import * as SearchDialog from './dialogs/SearchDialog';
import * as Canonicalizer from './model/Canonicalizer';
import * as CanonicalizerBuilder  from './model/CanonicalizerBuilder';
import * as EnglishWordAnalyzer from './model/WordAnalyzer';
import * as FilterExpression from './model/FilterExpression';
import * as SearchQueryBuilder from './search/SearchQueryBuilder';
import * as AzureSearchClient from './search/AzureSearchClient';
import * as Keywords from './model/Keywords';
import * as FilterExpressionBuilder from './model/FilterExpressionBuilder';
import * as ComparisonSpecification from './model/ComparisonSpecification';
import * as Ranges from './model/Ranges';

declare var exports: any;

exports.SearchDialog = SearchDialog.SearchDialog;
exports.Canonicalizer = Canonicalizer.Canonicalizer;
exports.FilterExpression = FilterExpression.FilterExpression;
exports.Operator = FilterExpression.Operator;
exports.SearchQueryBuilder = SearchQueryBuilder.SearchQueryBuilder;
exports.AzureSearchClient = AzureSearchClient.AzureSearchClient;
exports.CanonicalizerBuilder = CanonicalizerBuilder.CanonicalizerBuilder;
exports.EnglishWordAnalyzer = EnglishWordAnalyzer.EnglishWordAnalyzer;
exports.Keywords = Keywords.Keywords;
exports.FilterExpressionBuilder = FilterExpressionBuilder.FilterExpressionBuilder;
exports.ComparisonSpecification = ComparisonSpecification.ComparisonSpecification;
exports.Ranges = Ranges.Ranges;