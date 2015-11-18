
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTM.Domain
{
    public class Repository
    {
        private EFDatabase _db = null;

        public Repository(string connectionString)
        {
            _db = new EFDatabase(connectionString);
        }

        public Task<Show[]> GetShowsAsync()
        {
            return _db.Shows.ToArrayAsync();
        }

        public Task AddPurchasedTicketsAsync(PurchaseRequest request)
        {
            foreach (var ticket in request.DesiredTickets)
            {
                _db.Tickets.Add(ticket);
            }

            return _db.SaveChangesAsync();
        }

        public Task AddAuditRecord(AuditRecord record)
        {
            _db.AuditRecords.Add(record);
            return _db.SaveChangesAsync();
        }
    }

    class EFDatabase : DbContext
    {
        public EFDatabase(string connectionString)
            : base(connectionString)
        {
        }

        public DbSet<Show> Shows { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }
    }
}
