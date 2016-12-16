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

import { IRecognizeDialogContext, IntentDialog, IIntentDialogOptions, Session, IDialogResult, IIntentRecognizerResult, IIsCardAction, CardAction } from 'botbuilder';
import { ISearchHit, ISearchResult, IFacet } from '../search/ISearchClient';
import { ISearchSchema, ISearchField } from '../search/ISearchSchema';
import { ISearchSpecification } from './SearchSpecificationManager';
import { ISearchQuery } from '../search/ISearchQuery';
import { PreferredFilters } from '../search/SearchConstants';
import { AzureSearchClient } from '../search/AzureSearchClient';
import { SearchQueryBuilder } from '../search/SearchQueryBuilder';
import { SearchSpecificationManager } from './SearchSpecificationManager';
import { ISearchClient } from '../search/ISearchClient';
import { SearchSpecDescriptor } from './SearchSpecDescriptor';
import { IDialogCanonicalizers, CanonicalizerBuilder } from '../model/CanonicalizerBuilder';
import { FacetResultReader } from './FacetResultReader';
import { Controls } from './Controls';
import { DialogAction } from './DialogAction';
import { EntityTypes } from '../model/EntityTypes'; 
import { StringBuilder } from '../tools/StringBuilder';
import * as sprintf from 'sprintf-js';

export var SearchIntents = {
    Empty: '',
    Facet: 'Facet',
    Filter: 'Filter',
    NextPage: 'NextPage',
    Refine: 'Refine',
    List: 'List',
    StartOver: 'StartOver',
    Quit: 'Quit',
    Done: 'Done',
    AddList: 'AddList',
    RemoveList: 'RemoveList'
};

export interface ISearchDialogOptions {
    searchServiceUrl: string;
    searchServiceKey: string;
    searchIndexName: string;
}

export class SearchDialog extends IntentDialog {

    /**
     * Members
     */ 

    private client: ISearchClient;
    private searchQueryBuilder: SearchQueryBuilder;
    private searchSpecManager: SearchSpecificationManager;
    private facetProcessor: FacetResultReader;
    
    private defaultProperty: string;
    
    private resultMapHandler: (searchResult: any) => ISearchHit;
    private searchSchema: ISearchSchema;

    private previousButtons: IIsCardAction[];
    private latestHits: ISearchHit[];
    private selection: ISearchHit[];
    private refinersOverride: string[];

    /**
     * Construction
     */ 

    constructor(private options: ISearchDialogOptions)  {
        super();
        this.registerIntents();

        this.selection = [];
        this.defaultProperty = null;
    }

    /**
     * Fluent properties
     */ 

    public schema(schema: ISearchSchema): this {
        this.searchSchema = schema; 

        if(this.canInitialize()) {
            this.initialize();
        }
        return this;
    }

    public onResultMapping(handler: (searchResult: any) => ISearchHit): this {
        this.resultMapHandler = handler;

        if(this.canInitialize()) {
            this.initialize();
        }
        return this;
    }

    public searchClient(client: ISearchClient): this {
        this.client = client;
        
        if(this.canInitialize()) {
            this.initialize();
        }
        return this;
    }

    public refiners(refiners: string[]): this {
        this.refinersOverride = refiners;
        return this;
    }

    public recognize(context: IRecognizeDialogContext, cb: (err: Error, result: IIntentRecognizerResult) => void): void {
        
        // Check if schema() and onResultMapping() were called to set the search schema and the onResultMapping handler. Without those, we can't start receiving messages.
        this.validateDialogState();

        // Intercept before calling recognizers to check for add or remove messages and handle those here. For other messages, we let IntentDialog super class handle it.
        if(context.message && context.message.text) {
            let text: string = context.message.text.trim();
            
            // Check whether the user is trying to add an item to the list.
            const add = 'ADD:';
            let indexOfAdd: number = text.indexOf(add);

            if(indexOfAdd == 0) {
                let key = text.substring(add.length).trim();
                //this.selectItem(context, key);
                
                // It was an Add, so we just recognize this here. We pass the key as a recognized entity so the intent handler for AddList can retrieve the key to add.
                cb(null, { intent: SearchIntents.AddList, score: 1.0, entities: [ { type: EntityTypes.Value, entity: key }]});

                return; 
            }

            // Check whether the user is trying to add an item to the list
            const remove = 'REMOVE:';
            let indexOfRemove: number = text.indexOf(remove);

            if(indexOfRemove == 0) {
                let key = text.substring(remove.length).trim();
                //this.removeItem(context, key);
                
                // It was a Remove, so we just recognize this here. We pass the key as a recognized entity so the intent handler for RemoveList can retrieve the key to remove.
                cb(null, { intent: SearchIntents.RemoveList, score: 1.0, entities: [ { type: EntityTypes.Value, entity: key }]});
                return; 
            }
        }
        
        // It was not a remove or an add, just let this pass through the recognizers
        super.recognize(context, cb);
    }


    private search(session: Session) {

        let query: ISearchQuery = this.searchQueryBuilder.build(this.searchSpecManager.getSpec());

        this.client.search(query, (error: Error, searchResult: ISearchResult): void => {
            if(error) {
                session.error(error);
            } else {
                this.latestHits = searchResult.hits;
                // Show the results
                DialogAction.showResults(session, searchResult.hits, this.searchSpecManager.getSpec());

                // Send a keyboard with action suggestions to the user
                DialogAction.sendKeyboard(session, Controls.refineButtons(session), 'refine_prompt');
            }
        });
    }

    /**
     * Intent handler methods
     */ 

    private filter(session: Session, luisResponse: IIntentRecognizerResult) {
        
        this.searchSpecManager.filter(session.toRecognizeContext(), luisResponse.entities, session.message.text || '', this.defaultProperty);
        this.search(session);
        this.defaultProperty = null;
    }

    private facet(session: Session, luisResponse: IIntentRecognizerResult) {
        this.searchSpecManager.facet(luisResponse.entities, session.message.text || '', this.defaultProperty);

        let facet: string = this.searchSpecManager.getSpec().facet;

        // If no refiner was found, just filter
        if(!facet) {
            this.filter(session, luisResponse);
            return;
        }

        // We found the refiner. Now, build the response based on the filter preferences of the refiner fields
        let searchField: ISearchField = this.searchSchema.Fields[facet];
        let searchFieldSynonym = (searchField.NameSynonyms && searchField.NameSynonyms.Alternatives && searchField.NameSynonyms.Alternatives.length > 0) 
            ? searchField.NameSynonyms.Alternatives[0] : null;

        let buttons: IIsCardAction[] = [];
        
        if(searchField.FilterPreference != PreferredFilters.None) {
            let query: ISearchQuery = this.searchQueryBuilder.build(this.searchSpecManager.getSpec());

            // Execute search
            this.client.search(query, (error: Error, searchResult: ISearchResult): void => {
                if(error) {
                    session.error(error);
                } else {
                    // If we got facets back in the search results
                    if(searchResult && searchResult.facets[facet]) {
                        
                        buttons = this.facetProcessor.toButtons(session, searchResult.facets[facet], searchField, searchFieldSynonym);

                        if(buttons.length > 0) {
                            DialogAction.sendKeyboard(session, buttons, 'facet_value_prompt', searchFieldSynonym);
                        }
                        else {
                            DialogAction.sendMessage(session, 'no_values_prompt');
                        }
                    }
                }
            });
        }
        else {
            DialogAction.sendKeyboard(session, [CardAction.imBack(session, 'Any ' + searchFieldSynonym, 'Any ' + searchFieldSynonym)], 'filter_prompt', searchFieldSynonym)
            this.defaultProperty = facet;
        }
    }

    private nextPage(session: Session, luisResponse: IIntentRecognizerResult) {
        this.searchSpecManager.nextPage();
        this.search(session);
    }

    private refine(session: Session, luisResponse: IIntentRecognizerResult) {
        let refiners = this.getRefiners(session);
        DialogAction.sendKeyboard(session, refiners, 'facet_prompt');
    }

    private startOver(session: Session, luisResponse: IIntentRecognizerResult) {
        this.searchSpecManager.reset();
    }

    private quit(session: Session, luisResponse: IIntentRecognizerResult) {
        session.endDialog();
    }

    private done(session: Session, luisResponse: IIntentRecognizerResult) {
        session.endDialogWithResult({ response: this.selection });
    }

    private list(session: Session, luisResponse: IIntentRecognizerResult) {
        
        if(this.selection.length == 0) {
            DialogAction.sendMessage(session, 'not_added_prompt');
        }
        else {
            DialogAction.showResults(session, this.selection, this.searchSpecManager.getSpec(), true);
        }
    }

    /**
     * Dialog State methods  
     */ 

    private initialize() {

        if(!this.client) {
            this.client = new AzureSearchClient(this.options.searchServiceUrl, this.options.searchServiceKey, this.options.searchIndexName, this.searchSchema, this.resultMapHandler);
        }
        
        let canonicalizers: IDialogCanonicalizers = CanonicalizerBuilder.build(this.searchSchema);
        this.searchQueryBuilder = new SearchQueryBuilder();
        this.searchSpecManager = new SearchSpecificationManager(this.searchSchema, canonicalizers);
        this.facetProcessor = new FacetResultReader(canonicalizers);
        this.onBegin((session: Session, args: any, next: () => void): void => {
            let message = new StringBuilder();
            message.appendLine(session.localizer.gettext(session.preferredLocale(), 'welcome_prompt'));
            message.appendLine(session.localizer.gettext(session.preferredLocale(), 'initial_prompt'));

            DialogAction.sendKeyboard(session, [Controls.browse(session), Controls.quit(session)], message.toString());

            next();
        });
    }

    private canInitialize(): boolean {
        if(this.searchSchema && this.resultMapHandler)
            return true;
        return false;
    }

    private validateDialogState() {
        if(!this.canInitialize()) {
            throw new Error('Dialog cannot start receiving requests. Specify the search schema and search result mapping handler through the schema() and onResultMapping() functions.');
        }
    }

    private getRefiners(session: Session): IIsCardAction[] {
        let newRefiners: IIsCardAction[] = [];

        // If the caller specified their own refiners
        if(this.refinersOverride) {
            for(let fieldName of this.refinersOverride) {
                let field = this.searchSchema.Fields[fieldName];

                if(field && field.IsFacetable && field.NameSynonyms && field.NameSynonyms.Alternatives.length > 0) {
                    let description = field.NameSynonyms.Alternatives[0];
                    newRefiners.push(CardAction.postBack(session, description, description))
                }
            }
        }
        // If the refiners were not overriden by the callers, we obtain the refiners from the search schema
        else {
            for(let fieldKey in this.searchSchema.Fields) {
                let field = this.searchSchema.Fields[fieldKey];

                if(field.IsFacetable && field.NameSynonyms && field.NameSynonyms.Alternatives.length > 0) {
                    let description = field.NameSynonyms.Alternatives[0];
                    newRefiners.push(CardAction.postBack(session, description, description))
                }
            }
        }

        return newRefiners;
    } 

    /**
     * Selection list methods
     */ 

    private selectItem(session: Session, key: string) {

        if(!key) {
            return;
        }

        let candidateHits = this.latestHits.filter((value: ISearchHit, index: number, array: ISearchHit[]) => {
            return value.key == key 
        });

        // The requested item was not found, show a message to the user explaining we can't add it to the list
        if(!candidateHits || candidateHits.length == 0) {
            DialogAction.sendMessage(session, 'unknown_item_prompt');
        }
        else {
            
            let hit: ISearchHit = candidateHits[0];

            // Check if the item is already selected
            let selectedMatches = this.selection.filter((value: ISearchHit, index: number, array: ISearchHit[]) => {
                return value.key == key 
            });

            // If the item was not already selected, add it to the selected list
            if(!selectedMatches || selectedMatches.length == 0) {
                this.selection.push(hit);
                
                // Send confirmation to the user
                DialogAction.sendMessage(session, 'added_to_list_prompt', hit.title);
            }
        }
    }

    private removeItem(session: Session, key: string) {

        if(!key) {
            return;
        }

        let candidateHits = this.latestHits.filter((value: ISearchHit, index: number, array: ISearchHit[]) => {
            return value.key == key 
        });

        // The requested item was not found, show a message to the user explaining we can't remove it from the list
        if(!candidateHits || candidateHits.length == 0) {
            DialogAction.sendMessage(session, 'unknown_item_prompt');
        }
        else {
            
            let hit: ISearchHit = candidateHits[0];

            // Remove the hit from the selection list
            let hitIndex: number = -1;
            this.selection.forEach((value: ISearchHit, index: number, array: ISearchHit[]) => {
                if(value.key == key) {
                    hitIndex = index;
                } 
            });

            if(hitIndex != -1) {
                this.selection.splice(hitIndex, 1);
            }

            DialogAction.sendMessage(session, 'removed_from_list_prompt', hit.title);
        }
    }

    private registerIntents(): void {
        this
        .matches(SearchIntents.Empty, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Empty);
        })
        .matches(SearchIntents.Facet, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.facet(session, result);
        })
        .matches(SearchIntents.Filter, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.filter(session, result);
        })
        .matches(SearchIntents.NextPage, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.nextPage(session, result);
        })
        .matches(SearchIntents.Refine, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.refine(session, result);
        })
        .matches(SearchIntents.List, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.list(session, result);
        })
        .matches(SearchIntents.StartOver, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.startOver(session, result);
        })
        .matches(SearchIntents.Quit, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.quit(session, result);
        })
        .matches(SearchIntents.Done, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.done(session, result);
        })
        .matches(SearchIntents.AddList, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.selectItem(session, result.entities ? result.entities[0].entity : null);
        })
        .matches(SearchIntents.RemoveList, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            this.removeItem(session, result.entities ? result.entities[0].entity : null);
        })
        .onDefault((session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Done);
        });
    }
}

