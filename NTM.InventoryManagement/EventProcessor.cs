
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using NTM.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.InventoryManagement
{
    public class MyEventProcessor : IEventProcessor
    {
        private readonly Random _rand = new Random(Environment.TickCount);

        private Repository _repos = null;
        private Stopwatch _checkpointStopWatch = null;

        public MyEventProcessor()
        {
            var connectionString = CloudConfigurationManager.GetSetting("reposConnectionString");
            _repos = new Repository(connectionString);
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Trace.TraceInformation(string.Format("MyEventProcessor Shuting Down.  Partition '{0}', Reason: '{1}'.", context.Lease.PartitionId, reason.ToString()));

            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            Trace.TraceInformation(string.Format("MyEventProcessor initialize.  Partition: '{0}', Offset: '{1}'", context.Lease.PartitionId, context.Lease.Offset));

            _checkpointStopWatch = new Stopwatch();
            
            _checkpointStopWatch.Start();
            
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            var client = await InitTopic();

            foreach (EventData eventData in messages)
            {
                await Task.Delay(_rand.Next(100, 2000));        // spice of life

                var json = Encoding.UTF8.GetString(eventData.GetBytes());

                Trace.TraceInformation(string.Format("Message received.  Partition: '{0}', Data: '{1}'", context.Lease.PartitionId, json));

                var request = JsonConvert.DeserializeObject<PurchaseRequest>(json);

                var result = new PurchaseResult
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Request = request,
                    Id = Guid.NewGuid()
                };

                try
                {
                    await _repos.AddPurchasedTicketsAsync(request);
                    result.Success = true;
                }
                catch(Exception ex)
                {
                    result.Success = false;
                    result.ExceptionJson = JsonConvert.SerializeObject(ex);
                    result.FailureReason = PurchaseFailureReason.ArtistInRehab;
                }

                var topicMsg = new BrokeredMessage();

                topicMsg.Properties["json"] = JsonConvert.SerializeObject(result);
                topicMsg.CorrelationId = (string) eventData.Properties["correlationId"];

                await client.SendAsync(topicMsg);
            }

            if (_checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(5))
            {
                await context.CheckpointAsync();

                lock (this)
                {
                    _checkpointStopWatch.Reset();
                }
            }
        }

        private static async Task<TopicClient> InitTopic()
        {
            var connectionString = CloudConfigurationManager.GetSetting("topicConnectionString");
            var topicName = CloudConfigurationManager.GetSetting("topicName");

            var nsMgr = NamespaceManager.CreateFromConnectionString(connectionString);

            if (!await nsMgr.TopicExistsAsync(topicName))
            {
                await nsMgr.CreateTopicAsync(topicName);
            }

            return TopicClient.CreateFromConnectionString(connectionString, topicName);
        }
    }
}
