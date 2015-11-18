
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.Domain
{
    public class AuditRecord
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public DateTimeOffset RequestTimestamp { get; set; }
        public string JsonTickets { get; set; }
        public Guid ResultId { get; set; }
        public DateTimeOffset ResultTimestamp { get; set; }
        public bool Success { get; set; }
        public string FailureReason { get; set; }
    }
}
