
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

namespace NTM.Auditing
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private NamespaceManager _nsMgr = null;
        private SubscriptionClient _client = null;

        public override void Run()
        {
            Trace.TraceInformation("NTM.Auditing is running");

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

            if (!_nsMgr.SubscriptionExists(topicName, "audit"))
            {
                _nsMgr.CreateSubscription(topicName, "audit");
            }

            _client = SubscriptionClient.CreateFromConnectionString(connectionString, topicName, "audit", ReceiveMode.ReceiveAndDelete);

            var result = base.OnStart();

            Trace.TraceInformation("NTM.Auditing has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("NTM.Auditing is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            var topicName = CloudConfigurationManager.GetSetting("topicName");

            _nsMgr.DeleteSubscription(topicName, "audit");

            base.OnStop();

            Trace.TraceInformation("NTM.Auditing has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var reposConnectionString = CloudConfigurationManager.GetSetting("reposConnectionString");

            var repos = new Repository(reposConnectionString);

            while (!cancellationToken.IsCancellationRequested)
            {
                var msg = await _client.ReceiveAsync(TimeSpan.FromSeconds(5));

                if(msg != null)
                {
                    var json = (string) msg.Properties["json"];

                    var result = JsonConvert.DeserializeObject<PurchaseResult>(json);

                    var record = new AuditRecord
                    {
                        Id = Guid.NewGuid(),
                        RequestId = result.Request.Id,
                        RequestTimestamp = result.Request.Timestamp,
                        ResultId = result.Id,
                        ResultTimestamp = result.Timestamp,
                        Success = result.Success,
                        FailureReason = result.Success ? "<none>" : result.FailureReason.Value.ToString(),
                        JsonTickets = JsonConvert.SerializeObject(result.Request.DesiredTickets)
                    };

                    await repos.AddAuditRecord(record);
                }
            }
        }
    }
}
