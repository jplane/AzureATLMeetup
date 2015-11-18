
using Microsoft.AspNet.SignalR;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using NTM.Domain;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NotTicketMaster
{
    public class MainHub : Hub
    {
        private readonly Repository _repos = null;

        public MainHub()
        {
            var connectionString = ConfigurationManager.AppSettings["reposConnectionString"];
            _repos = new Repository(connectionString);
        }

        public async Task<IEnumerable<object>> GetShowInfo()
        {
            var shows = await _repos.GetShowsAsync();

            var results = new List<object>();

            foreach(var show in shows)
            {
                results.Add(new
                {
                    id = show.Id,
                    data = string.Format("{0} playing at {1} on {2} for {3:c}", show.Artist, show.Venue, show.Date.Date.ToShortDateString(), show.Price)
                });
            }

            return results.ToArray();
        }

        public async Task MakeTicketRequest(Guid showId, int ticketCount)
        {
            var connectionString = ConfigurationManager.AppSettings["ehConnectionString"];
            var eventHubName = ConfigurationManager.AppSettings["ehName"];

            try
            {
                var eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, eventHubName);

                var tix = new List<Ticket>();

                for (var i = 0; i < ticketCount; i++)
                {
                    tix.Add(new Ticket
                    {
                        Id = Guid.NewGuid(),
                        ShowId = showId
                    });
                }

                var request = new PurchaseRequest
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    DesiredTickets = tix.ToArray()
                };

                var json = JsonConvert.SerializeObject(request);

                var data = new EventData(Encoding.UTF8.GetBytes(json));

                data.PartitionKey = "NTM.UI";
                data.Properties["correlationId"] = Context.ConnectionId;    // preserve this so we can send a response later

                await eventHubClient.SendAsync(data);

                Clients.Caller.orderPlaced(new
                {
                    id = request.Id,
                    ticketcount = ticketCount
                });
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public static void SendResult(PurchaseResult result, string connectionId)
        {
            var hubContext = GlobalHost.ConnectionManager.GetHubContext<MainHub>();

            if(result.Success)
            {
                hubContext.Clients.Client(connectionId).orderSucceeded(new { id = result.Request.Id });
            }
            else
            {
                hubContext.Clients.Client(connectionId).orderFailed(new { id = result.Request.Id });
            }

            var summary = new
            {
                id = result.Id,
                requested = result.Request.Timestamp.ToString("u"),
                processed = result.Timestamp.ToString("u"),
                success = result.Success.ToString(),
                ticketcount = result.Request.DesiredTickets.Length
            };

            hubContext.Clients.All.requestProcessed(summary);
        }
    }
}
