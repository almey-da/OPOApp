using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class Transaction
    {
        public int Id { get; set; }
        public string TransactionName { get; set; } = null!;
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; } = null!;
        public string Amount { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
