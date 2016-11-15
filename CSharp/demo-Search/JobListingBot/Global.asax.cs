namespace JobListingBot
{
    using System.Web.Http;
    using Autofac;
    using Microsoft.Azure.Search.Models;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using JobListingBot.Dialogs;
    using Search.Azure.Services;
    using Search.Models;
    using Search.Services;
    using System.IO;
    using Newtonsoft.Json;
    using System.Web;

    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterType<JobListingSearchDialog>()
              .As<IDialog<object>>()
              .InstancePerDependency();

            builder.RegisterType<JobsMapper>()
                .Keyed<IMapper<DocumentSearchResult, GenericSearchResult>>(FiberModule.Key_DoNotSerialize)
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.Register((c) => JsonConvert.DeserializeObject<SearchSchema>(File.ReadAllText(Path.Combine(HttpContext.Current.Server.MapPath("/"), @"dialogs\JobListing.json"))))
                .InstancePerLifetimeScope();

            builder.RegisterType<AzureSearchClient>()
                .Keyed<ISearchClient>(FiberModule.Key_DoNotSerialize)
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.Update(Conversation.Container);

            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}
