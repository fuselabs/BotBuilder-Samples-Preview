
var searchBuilder = require('./lib/botsearch');
var builder = require('botbuilder');
var restify = require('restify');
var util = require('util');
var searchSchema = require("./RealEstate");

// Create chat bot
var connector = new builder.ChatConnector({
    appId: process.env.MICROSOFT_APP_ID,
    appPassword: process.env.MICROSOFT_APP_PASSWORD
});

var bot = new builder.UniversalBot(connector, {
    localizerSettings: { 
        botLocalePath: "./src/locale", 
        defaultLocale: "en" 
    }
});

var model = process.env.model || 'https://api.projectoxford.ai/luis/v1/application?id=40220979-7cf9-48ad-a995-710d646626be&subscription-key=e740e5ecf4c3429eadb1a595d57c14c5&q=';
var recognizer = new builder.LuisRecognizer(model);
 
var hitMapper = function(obj) {
    return {
        key: obj['listingId'],
        description: obj['description'],
        title: util.format('%d bedroom, %d bath in %s, %s, $%d', obj['beds'], obj['baths'], obj['city'], obj['region'].toUpperCase(), obj['price']),
        thumbnailUrl: obj['thumbnail']
    };
}

var searchDialogOptions = { 
    searchServiceUrl: 'https://realestate.search.windows.net',
    searchServiceKey: '82BCF03D2FC9AC7F4E9D7DE1DF3618A5',
    searchIndexName: 'listings'
}

var refiners = ["type", "beds", "baths", "sqft", "price", "city", "district", "region", "daysOnMarket", "status"]

var dialog = new searchBuilder.SearchDialog(searchDialogOptions)
    .schema(searchSchema)
    .onResultMapping(hitMapper)
    .recognizer(recognizer)
    .refiners(refiners);


bot.dialog('/', [
    function(session) {
        session.beginDialog('/realestate');
    },
    function(session, result){

        if(result && result.response && result.response.length > 0) {

            var text = 'Done! For future reference, you selected these properties: <br><br>';

            for(var propertyIndex in result.response) {
                text += '* ' + result.response[propertyIndex].title + ' (' + result.response[propertyIndex].key + ')<br>';
            }

            session.send(text);
        } else {
            session.send('Thanks for using the Real Estate Bot!');
        }
    }
]);

bot.dialog('/realestate', dialog)




var server = restify.createServer();
server.listen(process.env.port || process.env.PORT || 3978, function () {
   console.log('%s listening to %s', server.name, server.url); 
});

server.post('/api/messages', connector.listen());
