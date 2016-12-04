// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Bot Framework: http://botframework.com
// 
// Bot Builder SDK Github:
// https://github.com/Microsoft/BotBuilder
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

import * as SearchDialog from './dialogs/SearchDialog';
import * as Canonicalizer from './model/Canonicalizer';
import * as CanonicalizerBuilder  from './model/CanonicalizerBuilder';
import * as LocalizedWordAnalyzer from './model/LocalizedWordAnalyzer';
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
exports.LocalizedWordAnalyzer = LocalizedWordAnalyzer.LocalizedWordAnalyzer;
exports.Keywords = Keywords.Keywords;
exports.FilterExpressionBuilder = FilterExpressionBuilder.FilterExpressionBuilder;
exports.ComparisonSpecification = ComparisonSpecification.ComparisonSpecification;
exports.Ranges = Ranges.Ranges;