using System.IO;
using System.Web;
using System.Web.Http;
using Autofac;
using Microsoft.Azure.Search.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Internals.Fibers;
using Newtonsoft.Json;
using RealEstateBot.Dialogs;
using Search.Azure.Services;
using Search.Models;
using Search.Services;

namespace RealEstateBot
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<RealEstateSearchDialog>()
                .As<IDialog<object>>()
                .InstancePerDependency();

            builder.RegisterType<RealEstateMapper>()
                .Keyed<IMapper<DocumentSearchResult, GenericSearchResult>>(FiberModule.Key_DoNotSerialize)
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.Register(
                    (c) =>
                        JsonConvert.DeserializeObject<SearchSchema>(
                            File.ReadAllText(Path.Combine(HttpContext.Current.Server.MapPath("/"),
                                @"dialogs\RealEstate.json"))))
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