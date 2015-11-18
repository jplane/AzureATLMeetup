
using Microsoft.Owin;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using NTM.Domain;
using Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

[assembly: OwinStartup(typeof(NotTicketMaster.Startup))]

namespace NotTicketMaster
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            GlobalConfiguration.Configure(WebApiConfig.Register);

            HostingEnvironment.QueueBackgroundWorkItem(async ct =>
            {
                var connectionString = ConfigurationManager.AppSettings["topicConnectionString"];
                var topicName = ConfigurationManager.AppSettings["topicName"];

                var nsMgr = NamespaceManager.CreateFromConnectionString(connectionString);

                if (!nsMgr.TopicExists(topicName))
                {
                    nsMgr.CreateTopic(topicName);
                }

                if (!nsMgr.SubscriptionExists(topicName, "clientResponse"))
                {
                    nsMgr.CreateSubscription(topicName, "clientResponse");
                }

                var client = SubscriptionClient.CreateFromConnectionString(connectionString, topicName, "clientResponse", ReceiveMode.ReceiveAndDelete);

                while (!ct.IsCancellationRequested)
                {
                    var msg = await client.ReceiveAsync(TimeSpan.FromSeconds(5));

                    if (msg != null)
                    {
                        var json = (string) msg.Properties["json"];

                        var result = JsonConvert.DeserializeObject<PurchaseResult>(json);

                        MainHub.SendResult(result, msg.CorrelationId);
                    }
                }
            });
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
