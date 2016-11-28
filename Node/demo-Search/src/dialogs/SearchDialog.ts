
import { IntentDialog, IIntentDialogOptions, Session, IDialogResult, IIntentRecognizerResult, Message, ThumbnailCard, CardImage, AttachmentLayout } from 'botbuilder';
import { ISearchHit, ISearchResult} from '../search/ISearchClient';
import { ISearchSchema } from '../search/ISearchSchema';
import { ISearchSpecification } from './SearchSpecificationManager';
import { ISearchQuery } from '../search/ISearchQuery';
import { AzureSearchClient } from '../search/AzureSearchClient';
import { SearchQueryBuilder } from '../search/SearchQueryBuilder';
import { SearchSpecificationManager } from './SearchSpecificationManager';
import { ISearchClient } from '../search/ISearchClient';

export var SearchIntents = {
    Empty: '',
    Facet: 'Facet',
    Filter: 'Filter',
    NextPage: 'NextPage',
    Refine: 'Refine',
    List: 'List',
    StartOver: 'StartOver',
    Quit: 'Quit',
    Done: 'Done'
};

export interface ISearchDialogOptions extends IIntentDialogOptions {
    searchSchema: ISearchSchema;
    resultMapperCallback:  (searchResult: any) => ISearchHit;
    searchServiceUrl: string;
    searchServiceKey: string;
    searchIndexName: string;
    searchClientToInject?: ISearchClient;
}

export class SearchDialog extends IntentDialog {

    private readonly searchClient: ISearchClient;
    private readonly searchQueryBuilder: SearchQueryBuilder;
    private readonly searchSpecManager: SearchSpecificationManager;
    private defaultProperty: string;

    constructor(private options: ISearchDialogOptions)  {
        super(options);
        this.registerIntents();

        if(options.searchClientToInject) {
            this.searchClient = options.searchClientToInject;
        }
        else {
            this.searchClient = new AzureSearchClient(options.searchServiceUrl, options.searchServiceKey, options.searchIndexName, options.searchSchema, options.resultMapperCallback);
        }
        
        this.searchQueryBuilder = new SearchQueryBuilder();
    }

    private showResults(session: Session, hits: ISearchHit[]) {
        let searchSpec = this.searchSpecManager.getSpec();
        let message = new Message(session)
            .text(searchSpec.filter ? 'Filter: ' + searchSpec.filter.toUserFriendlyString() : 'No filter')
            //TODO: Add display of keywords, selection, etc
            .attachmentLayout(AttachmentLayout.carousel);

        hits.forEach((hit: ISearchHit) => {
             message.addAttachment(
                new ThumbnailCard(session)
                    .title(hit.title)
                    .text(hit.description)
                    .images([
                        CardImage.create(session, hit.thumbnailUrl)
                    ])
             )
        })

        session.send(message);
    }

    private filter(session: Session, luisResponse: IIntentRecognizerResult): void {

        this.defaultProperty = null;
        this.searchSpecManager.filter(luisResponse.entities, session.message.text, this.defaultProperty);

        let query: ISearchQuery = this.searchQueryBuilder.build(this.searchSpecManager.getSpec());

        this.searchClient.search(query, (searchResult: ISearchResult, error: Error): void => {
            if(error) {
                //Best way of showing errors to users?
                throw error;
            } else {
                this.showResults(session, searchResult.hits);
            }
        });
    }

    private facet(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private nextPage(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private refine(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private startOver(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private quit(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private done(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private list(session: Session, luisResponse: IIntentRecognizerResult): void {
        //TODO: Implement
    }

    private registerIntents(): void {
        this
        .matches(SearchIntents.Empty, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Empty);
        })
        .matches(SearchIntents.Facet, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Facet);
            this.facet(session, result);
        })
        .matches(SearchIntents.Filter, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Filter);
            this.filter(session, result);
        })
        .matches(SearchIntents.NextPage, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.NextPage);
        })
        .matches(SearchIntents.Refine, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Refine);
        })
        .matches(SearchIntents.List, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.List);
        })
        .matches(SearchIntents.StartOver, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.StartOver);
        })
        .matches(SearchIntents.Quit, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Quit);
        })
        .matches(SearchIntents.Done, (session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Done);
        })
        .onDefault((session: Session, result?: IIntentRecognizerResult, skip?: (results?: IDialogResult<any>) => void): void => {
            console.log(SearchIntents.Done);
        });
    }
}

