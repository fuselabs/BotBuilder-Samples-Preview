
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

var bot = new builder.UniversalBot(connector);

var model = process.env.model;
var recognizer = new builder.LuisRecognizer(model);
 
var hitMapper = function(obj) {
    return {
        key: obj['listingId'],
        description: obj['description'],
        title: util.format('%d bedroom, %d bath in %s, %s, $%d', obj['beds'], obj['baths'], obj['city'], obj['region'].toUpperCase(), obj['price']),
        thumbnailUrl: obj['thumbnail']
    };
}

var dialog = new searchBuilder.SearchDialog({ 
    recognizers: [recognizer],
    searchServiceUrl: 'https://realestate.search.windows.net',
    searchServiceKey: '82BCF03D2FC9AC7F4E9D7DE1DF3618A5',
    searchIndexName: 'listings',
    resultMapperCallback: hitMapper
});

bot.dialog('/', dialog);

//TODO: Support console & emulator here

var server = restify.createServer();
server.listen(process.env.port || process.env.PORT || 3978, function () {
   console.log('%s listening to %s', server.name, server.url); 
});
  

server.post('/api/messages', connector.listen());
