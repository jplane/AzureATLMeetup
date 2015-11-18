
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.Domain
{
    public class PurchaseResult
    {
        public Guid Id { get; set; }
        public PurchaseRequest Request { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public bool Success { get; set; }
        public PurchaseFailureReason? FailureReason { get; set; }
        public string ExceptionJson { get; set; }
    }

    public enum PurchaseFailureReason
    {
        CreditCard = 1,
        VenueFull,
        ShowCancelled,
        ArtistInRehab
    }
}
