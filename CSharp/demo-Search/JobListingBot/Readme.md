In order to generate a LUIS application for RealEstateBot.
1) Extract schema information from Azure Search: Search.Tools.Extract azs-playground nycjobs <adminKey> -g JobListingBot-histogram.bin -v posting_date -h JobListingBot-histogram.bin -o ..\..\..\JobListingBot\Dialogs\JobListingBot.json -f agency,business_title,civil_service_title,tags
2) Set the environment variable LUISSubscriptionKey = <your LUIS subscription key> or specify with -l.
3) Generate and upload the model with: Search.Tools.Generate .\..\JobListingBot\Dialogs\JobListing.json -o ..\..\JobListingBot\Dialogs\JobsListingModel.json  -u
