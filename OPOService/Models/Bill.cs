using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class Bill
    {
        public int Id { get; set; }
        public string Virtualaccount { get; set; } = null!;
        public string Bills { get; set; } = null!;
        public string PaymentStatus { get; set; } = null!;
        public int TransactionId { get; set; }
    }
}
