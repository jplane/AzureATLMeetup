
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
using Microsoft.ServiceBus.Messaging;

namespace NTM.InventoryManagement
{
    public class WorkerRole : RoleEntryPoint
    {
        private EventProcessorHost _host = null;

        public override bool OnStart()
        {
            ServicePointManager.DefaultConnectionLimit = 12;

            var hostName = CloudConfigurationManager.GetSetting("ehHostName");
            var connectionString = CloudConfigurationManager.GetSetting("ehConnectionString");
            var hubName = CloudConfigurationManager.GetSetting("ehName");
            var storageConnectionString = CloudConfigurationManager.GetSetting("storageConnectionString");

            _host = new EventProcessorHost(hostName, hubName, EventHubConsumerGroup.DefaultGroupName, connectionString, storageConnectionString);
            
            _host.PartitionManagerOptions = new PartitionManagerOptions
            {
                MaxReceiveClients = 1    // easy to debug... bad for scalability  :-)
            };

            _host.RegisterEventProcessorAsync<MyEventProcessor>().Wait();

            Trace.TraceInformation("NTM.InventoryManagement has been started");

            return base.OnStart();
        }

        public override void OnStop()
        {
            _host.UnregisterEventProcessorAsync().Wait();

            Trace.TraceInformation("NTM.InventoryManagement has stopped");

            base.OnStop();
        }
    }
}
