
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.Domain
{
    public class PurchaseRequest
    {
        public Guid Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Ticket[] DesiredTickets { get; set; }
    }
}
