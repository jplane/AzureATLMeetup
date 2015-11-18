
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.Domain
{
    public class Show
    {
        public Guid Id { get; set; }
        public string Artist { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Venue { get; set; }
        public decimal Price { get; set; }
    }
}
