
import {IntentDialog, IIntentDialogOptions, IEntity} from 'botbuilder';

//=============================================================================
//
// INTERFACES
//
//=============================================================================

/** Options used to configure a SearchDialog. */
export interface ISearchDialogOptions extends IIntentDialogOptions {

    /** Azure Search schema json. */
    searchSchema: ISearchSchema;
    
    /** Callback that converts a search result object to an ISearchHit for display in the search dialog. */
    resultMapperCallback:  (searchResult: any) => ISearchHit;
    
    /** Url for the Azure Search service. */
    searchServiceUrl: string;
    
    /** Service Key for the Azure Search service. */
    searchServiceKey: string;
    
    /** Name of the Azure Search index. */
    searchIndexName: string;
    
    /** Optional ISearchClient implementation. If specified, the ISearchClient specified will be used for searching instead of Azure Search. */
    searchClientToInject?: ISearchClient;
}

/** Search Client definition*/
export interface ISearchClient {
    /** 
     * Performs a search based o the query specified.
     * @param query Query definition for the search.
     * @param callback Function to invoke once search is completed.
     * @param callback.searchResult Result of the search or `null` if there was an error.
     * @param callback.error Any error that occured or `null` if the call was successfull.
     */
    search(query: ISearchQuery, callback: (searchResult: ISearchResult, error: Error) => void): void;
}

/** Definition of a set of synonyms for a given canonical value. */
export interface ISynonyms {
    /** Canonical value. */
    Canonical: string;
    
    /** Alternative expressions for the canonical. */
    Alternatives: string[];
}

/** Definition of a facet in the context of Azure Search. */
export interface IFacet {
    /** Count for the facet. */
    count: number;
    
    /** Value of the facet. */
    value: string;
}

/** Definition of a search hit from a search service. */
export interface ISearchHit {
    /** Key that identifies the search hit. */
    key: string;

    /** Title of the search hit. */
    title: string;

    /** Url to a thumbnail representing the search hit. */
    thumbnailUrl: string;

    /** Description of the search hit. */
    description:string;
}

/** Result from a search execution. */
export interface ISearchResult {
    /** Hits obtained from the search or `null` if no records were found. */
    hits?: ISearchHit[];

    /** Facets obtained from the search of `null` if no facets were found or requested. */
    facets?: {[key: string]: IFacet[]};
}

/** Definition of a query to a search service. */
export interface ISearchQuery {
    /** Whether to only count the results. */
    count: boolean;

    /** List of facets to be queried. */
    facets: string[];

    /** Expression filter for the search. */
    filter: string;

    /** Keywords to be searched. */
    search: string;

    /** Fields to be included in the search. */
    searchFields: string;

    /** Search mode. Can take values 'any' or 'all' for Azure Search. */
    searchMode: string;

    /** Number of records to Skip. */
    skip: number;

    /** Number of records to retrieve. */
    top: number;

    /** Type of query to execute. Can take values 'simple' or 'full' in Azure Search. */
    queryType: string;
}

/** Definition of a search schema. */
export interface ISearchSchema {
    /** Map of search fields. */
    Fields?: {[key: string]: ISearchField};

    /** Property to which to map currency values by default. */
    DefaultCurrencyProperty?: string;

    /** Property to which to map numeric values by default. */
    DefaultNumericProperty?: string;

    /** Property to which to map geographic values by default. */
    DefaultGeoProperty?: string;

    /** Optional fragments. */
    Fragments?: any;
}

/** Definition of a search field to be used in calls to search services. */
export interface ISearchField {
    /** Name of the search field. */
    Name: string;

    /** Type of the search field. */
    Type: string;

    /** Whether the field is facetable. */
    IsFacetable: boolean;

    /** Whether the field is filterable. */
    IsFilterable: boolean;

    /** Whether the field is a key. */
    IsKey: boolean;

    /** Whether the field is retrievable. */
    IsRetrievable: boolean;

    /** Whether the field is searchable. */
    IsSearchable: boolean;

    /** Whether the field is sortable. */
    IsSortable: boolean;

    /** Filter preferences for the field. */
    FilterPreference: string;

    /** Name synonyms for the field's name. */
    NameSynonyms?: ISynonyms;

    /** Synonyms for the possible values the field can have. */
    ValueSynonyms?: ISynonyms[];
}

/** Definition of a search specification, which is used to store the search state in the SearchDialog. */
export interface ISearchSpecification {

    /** Phrases to be included in the search. */
    phrases: string[];

    /** Filter expression to be included in the search. */
    filter: FilterExpression;

    /** Current user's selection. */
    selection: string[];

    /** Number of records to skip. */
    skip?: number;

    /** Number of records to retrieve. */
    top?: number;

    /** Facet to use when searching. */
    facet?: string;
}

/** Definition of a retry strategy for any operation that has potential to fail intermittently. */
export interface IRetryStrategy {
    /** 
     * Executes an operation using a specific retry strategy to determine when to retry and the interval between retries.
     * @param predicate The operation to be executed by the retry strategy
    */
    execute<TResult>(predicate: () => TResult): TResult;
}

/** Definition of a canonical value. */
export interface ICanonicalValue {
    /** Search field. */
    field: ISearchField;
    
    /** Canonical value. */
    value: string;
    
    /** Description of the canonical value. */   
    description: string;
}

/** Group of field and value canonicalizers used together for dialog text canonicalization. */
export interface IDialogCanonicalizers {
    /** Canonicalizer for the names of a search field. */
    fieldCanonicalizer: Canonicalizer;

    /** Canonicalizer for the values of search fields. */
    valueCanonicalizers: {[value: string]: ICanonicalValue};
}

/** Range of a search field, used to determine which comparisons should be associated with it when building a search query for the search field. */
export interface IRange {

    /** The search field for which the range is computed. */
    property: ISearchField,

    /** Lower comparison range. */
    lower: any,

    /** Upper comparison range. */
    upper: any,

    /** Whether the lower bound should be specified in comparisons. */
    includeLower: boolean,

    /** Whether the upper bound should be specified in comparisons. */
    includeUpper: boolean,

    /** Description of the range. */
    description: string
}

/** Definition of a range of characters within a string. */
export interface IStringRange {

    /** Start index within the string. */
    start: number;

    /** End index within the string. */
    end: number;
}

/** Definition of a word analyzer that can detect different attributes from words. */
export interface IWordAnalyzer {

    /** 
     * Whether the specified word is a noise word. 
     * @param word The word to be analyzed.
    */
    isNoiseWord(word: string): boolean;

    /** 
     * Whether the specified word is a punctuation symbol.
     * @param word The word to be analyzed.
    */
    isPunctuation(word: string): boolean;
}

/** Definition of a number that can be currency or not. */
export interface ICurrencyNumber {

    /** Numeric value of the currency number. */
    value: number;

    /** Whether the number is actually currency or not. */
    isCurrency: boolean;
}

/** Grouping of filters that are used when building a search query. */
export interface IExpressionAndFullTextFilter {
    /** Filter generated from comparison expressions. */
    expressionFilter: FilterExpression;

    /** Filters generated for full-text searches. */
    fullTextFilter: FilterExpression[];
}


//=============================================================================
//
// Classes
//
//=============================================================================

/** Identifies a user's intents and uses Azure Search to retrieve results. */
export class SearchDialog extends IntentDialog {
    /** 
     * Constructs a new instance of a SearchDialog.
     * @param options Options used to initialize the dialog.
    */
    constructor(options: ISearchDialogOptions);
}

/** Abstraction responsible for managing the search specification to be used to query the search service, based on the user's intents. */
export class SearchSpecificationManager {
    /** 
     * Constructs a new instance of a SearchSpecificationManager.
     * @param searchSchema Azure search schema that defines fields, synonyms and search attributes.
    */
    constructor(searchSchema: ISearchSchema);

    /** 
     * Filters the current search specification based on the entities returned from the recognizers and the original text entered by the user.
     * @param entities Entities returned from the recognizers based on the user's intent.
     * @param originalText Text originally entered by the user for further analysis.
     * @param defaultProperty Default property to be considered for searching. 
    */
    filter(entities: IEntity[], originalText: string, defaultProperty?: string): void;

    /** Retrieves the current search specification. */
    getSpec(): ISearchSpecification;

    /** Retrieves the default search specification. */
    getDefaultSpec(): ISearchSpecification;
}

/** 
 * Expression operators enumeration.
*/
export enum Operator {
    None, LessThan, LessThanOrEqual, Equal, GreaterThanOrEqual, GreaterThan, And, Or, Not, FullText
}

/** Definition of a filter expression tree, used to represent a query based on the user's requests, which is then translated to a text search query. */
export class FilterExpression {
    /** 
     * Constructs an instance of a FilterExpression.
     * @param description Description of the filter expression.
     * @param operator Operator of the expression.
     * @param args Values among which the operator will be applied.
    */
    constructor(description: string, operator: Operator,  ...args:any[]);

    /** 
     * Combines two filter expressions and returns the resulting expression.
     * @param left A filter expression.
     * @param right A filter expression.
     * @param operator Operator to be applied to both specified expressions.
     * @param description Description of the new expression.
     * @returns The combined expression.
    */
    static combine(left: FilterExpression, right: FilterExpression, operator: Operator, description: string): FilterExpression;
    
    /**
     * Traverses the expression tree recursively in pre-order fashion, allowing callers to specify a visitor that is called on each node throughout the traversal.
     * @param node Root node from which to start the traversal.
     * @param nodeVisitor Visitor function that will be called on each node throughout the traversal.
     * @param nodeVisitor.node Node being visited at the time of calling the nodeVisitor.
    */
    static traversePreOrder(node: FilterExpression, nodeVisitor: (node: FilterExpression) => boolean): void;

    /** 
     * Traverses the expression tree recursively in a post-order fashion, allowing callers to specify a visitor that is called on each node throughout the traversal.
     * @param node Root node from which to start the traversal.
     * @param nodeVisitor Visitor function that will be called on each node throughout the traversal.
     * @param nodeVisitor.node Node being visited at the time of calling the nodeVisitor.
     * @param nodeVisitor.childrenResults results from the children of the current node.
     * @returns The result from the root node's execution of the nodeVisitor.
    */
    static traversePostOrder<TResult>(node: FilterExpression, nodeVisitor: (node: FilterExpression, childrenResults: TResult[]) => TResult): TResult;
    
    /**
     * Removes the specified expression from the expression tree.
     * @param expression The expression to be removed from the tree.
     * @returns The resulting expression tree with the specified expression removed from it.
     */
    remove(expression: FilterExpression): FilterExpression;

    /** 
     * Retrieves all search fields from the expression tree.
     * @returns An array of distinct search fields found in the expression tree.
     */
    retrieveSearchFields(): ISearchField[];

    /** 
     * Removes a search field from an expression tree.
     * @param fieldToRemove the field to be removed from the expression tree.
     * @returns The resulting expression tree with all the occurrences of the specified field removed from it.
     */
    removeSearchField(fieldToRemove: ISearchField): FilterExpression;

    /** Returns a user-readable description of the expression filter. */
    toUserFriendlyString(): string ;

    /** Getter for the operator of the filter expression. */
    getOperator(): Operator;
    
    /** Getter for the values of the filter expression. */
    getValues(): any[];
}

/** Canonicalizer abstraction used throughout the project to retrieve canonical values from user-entered words. */
export class Canonicalizer {
    
    /** 
     * Constructs an instance of a Canonicalizer
     * @param synonymsCollection (Optional) collection of synonyms to be used when canonicalizing.
    */
    constructor(synonymsCollection?: ISynonyms[]);

    /** 
     * Obtains the canonical value of a string.
     * @param source The string to be canonicalized.
     * @returns the canonical of the string or `null` if a canonical was not found.
    */
    canonicalize(source: string): string;

    /** 
     * Adds a synonym to the canonicalizer.
     * @param synonyms A synonym that will be added to the canonicalizer. 
    */
    addSynonyms(synonyms: ISynonyms): void;
}

/** Abstraction to build canonicalizers based on a search schema. */
export class CanonicalizerBuilder {
    /** 
     * Builds a grouping of field and value canonicalizers given a search schema.
     * @param searchSchema the schema that will be used to extract the field and value synonyms.
     * @returns A grouping of canonicalizers for values and fields built based upon the search schema.
    */
    static build(searchSchema: ISearchSchema): IDialogCanonicalizers;
}

/** Specification of a comparison based on recognizer entities. */
export class ComparisonSpecification {
    /** 
     * Constructs an instance of ComparisonSpecification.
    */
    constructor(entity: IEntity);
    
    /** 
     * Adds an entity to the comparison specification.
     * @param entity Entity to be added to the comparison specification.
     */
    addEntity(entity: IEntity): void;
    
    /** Gets the lower bound of the comparison specification. */
    getLower(): IEntity;
    
    /** Gets the upper bound of the comparison specification. */
    getUpper(): IEntity;
    
    /** Gets the operator of the comparison specification. */
    getOperator(): IEntity;
    
    /** Gets the property of the comparison specification. */
    getProperty(): IEntity;
    
    /** Gets the entity of the comparison specification. */
    getEntity(): IEntity;
}

/** Abstraction to build filter expressions based on recognizer's results. */
export class FilterExpressionBuilder {
    /**
     * Constructs an instance of a FilterExpressionBuilder.
     * @param canonicalizers Grouping of field and value canonicalizers to be used when building filter expressions.
     * @param schema The search schema for the application.
     */
    constructor(canonicalizers: IDialogCanonicalizers, schema: ISearchSchema);

    /**
     * Builds an instance of FilterExpression based on recognizer's results.
     * @param entities Collection of entities obtained from a recognizer.
     * @param originalText Original text entered by the user.
     * @param defaultProperty Property to be used in search.
     * @returns The resulting FilterExpression.
     */
    build(entities: IEntity[], originalText: string, defaultProperty?: string): FilterExpression;
}

/** Abstraction to extract phrases from a user-entered text considering a set of entities obtained from a recognizer. */
export class Keywords {
    /** 
     * Extract phrases from user-entered text based on entities obtained from a recognizer and language stop words and punctuation.
     * @param entities Entities obtained from a recognizer for the user's original text.
     * @param originalText Original text entered by the user.
     * @param analyzer Word analyzer to detect noise words and punctuation when extracting keywords.
     * 
    */
    static phrases(entities: IEntity[], originalText: string, analyzer: IWordAnalyzer): string[];
}

/** Determines attributes of words for the English language. */
export class EnglishWordAnalyzer {
    /** 
     * Constructs an instance of EnglishWordAnalyzer.
    */
    constructor();

    /** 
     * Whether the word is a noise word.
     * @param word The word to be tested.
    */
    isNoiseWord(word: string): boolean;

    /**
     * Whether the word is a punctuation character.
     * @param word The word to be tested.
     */
    isPunctuation(word: string): boolean;
}


/** Normalizes strings for canonicalization. */
export class Normalizer {
    /** 
     * Normalizes a string for canonicalization.
     * @param source The string to be normalized.
    */
    static normalize(source: string): string;
}

/** Obtains concrete ranges to build a query from, based on entity comparison entities obtained from a recognizer. */
export class Ranges {
    /** 
     * Constructs and instance of Ranges.
     * @param schema The search schema for the application.
     * @param canonicalizers Grouping of field and value canonicalizers.
    */
    constructor(schema: ISearchSchema, canonicalizers: IDialogCanonicalizers);

    /** 
     * Resolves the range for a given comparison based on the original text and default property.
     * @param comparison The comparison specification for which to obtain the range.
     * @param originalText Original text entered by the user.
     * @param defaultProperty (Optional) Default search property.
    */
    resolve(comparison: ComparisonSpecification, originalText: string, defaultProperty?: string): IRange;
}

/** Search client based on Azure Search. */
export class AzureSearchClient implements ISearchClient {
    /** 
     * Constructs an instance of AzureSearchClient.
     * @param service The service Url for the search service.
     * @param serviceKey The service key for the search service.
     * @param indexName The name of the index to be used for searching.
     * @param mapper A callback to map search results to ISearchHit.
     * @param mapper.searchResult The search result obtained from AzureSearch.
    */
    constructor(service: string, serviceKey: string, indexName: string, searchSchema: ISearchSchema, mapper: (searchResult: any) => ISearchHit);
    
    /** 
     * Searches using AzureSearch REST Api.
     * @param query The query to be issued to Azure Search service.
     * @param callback Callback function to be called after completion of the search execution.
     * @param callback.searchResult Search result returned from Azure Search or potentially `null` if the call failed.
     * @param callback.error Any errors found while calling Azure Search, or `null` if the call succeeded.
    */
    search(query: ISearchQuery, callback: (searchResult: ISearchResult, error: Error) => void);
}

/** Builds a search query based on filters, phrases and parameters of an ISearchSpecification. */
export class SearchQueryBuilder {
    /** 
     * Constructs an instance of SearchQueryBuilder.
    */
    constructor();

    /** 
     * Builds a search query ready to send to Azure Search from a search specification obtained analyzing the user's intents and conversation state.
     * @param searchSpec The search specification to be used to build the query.
     * @returns A search query based on the search specification provided. 
     */
    build(searchSpec: ISearchSpecification): ISearchQuery;
}

