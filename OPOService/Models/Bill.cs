using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class Bill
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Virtualaccount { get; set; } = null!;
        public string Bills { get; set; } = null!;
        public string PaymentStatus { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
