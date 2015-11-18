
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using NTM.Domain;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents;

namespace NTM.Analytics
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private NamespaceManager _nsMgr = null;
        private SubscriptionClient _client = null;

        public override void Run()
        {
            Trace.TraceInformation("NTM.Analytics is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            ServicePointManager.DefaultConnectionLimit = 12;

            var connectionString = CloudConfigurationManager.GetSetting("topicConnectionString");
            var topicName = CloudConfigurationManager.GetSetting("topicName");

            _nsMgr = NamespaceManager.CreateFromConnectionString(connectionString);

            if (!_nsMgr.TopicExists(topicName))
            {
                _nsMgr.CreateTopic(topicName);
            }

            if (!_nsMgr.SubscriptionExists(topicName, "analytics"))
            {
                _nsMgr.CreateSubscription(topicName, "analytics");
            }

            _client = SubscriptionClient.CreateFromConnectionString(connectionString, topicName, "analytics", ReceiveMode.ReceiveAndDelete);

            var result = base.OnStart();

            Trace.TraceInformation("NTM.Analytics has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("NTM.Analytics is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            var topicName = CloudConfigurationManager.GetSetting("topicName");

            _nsMgr.DeleteSubscription(topicName, "analytics");

            base.OnStop();

            Trace.TraceInformation("NTM.Analytics has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var endpointUrl = CloudConfigurationManager.GetSetting("ddbEndpointUri");
            var authKey = CloudConfigurationManager.GetSetting("ddbAuthKey");
            var dbName = CloudConfigurationManager.GetSetting("ddbName");
            var dbCollection = CloudConfigurationManager.GetSetting("ddbCollection");

            var dbClient = new DocumentClient(new Uri(endpointUrl), authKey);

            var db = GetOrCreateDatabase(dbClient, dbName);

            var coll = GetOrCreateCollection(dbClient, db.SelfLink, dbCollection);

            while (!cancellationToken.IsCancellationRequested)
            {
                var msg = await _client.ReceiveAsync(TimeSpan.FromSeconds(5));

                if (msg != null)
                {
                    var json = (string) msg.Properties["json"];

                    var result = JsonConvert.DeserializeObject<PurchaseResult>(json);

                    await dbClient.CreateDocumentAsync(coll.DocumentsLink, result);
                }
            }
        }

        private static DocumentCollection GetOrCreateCollection(DocumentClient client, string databaseLink, string collectionId)
        {
            var col = client.CreateDocumentCollectionQuery(databaseLink)
                              .Where(c => c.Id == collectionId)
                              .AsEnumerable()
                              .FirstOrDefault();

            if (col == null)
            {
                col = client.CreateDocumentCollectionAsync(databaseLink, new DocumentCollection { Id = collectionId }).Result;
            }

            return col;
        }

        private static Database GetOrCreateDatabase(DocumentClient client, string databaseId)
        {
            var db = client.CreateDatabaseQuery()
                            .Where(d => d.Id == databaseId)
                            .AsEnumerable()
                            .FirstOrDefault();

            if (db == null)
            {
                db = client.CreateDatabaseAsync(new Database { Id = databaseId }).Result;
            }

            return db;
        }
    }
}
