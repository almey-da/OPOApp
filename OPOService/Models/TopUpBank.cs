using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class TopUpBank
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Amount { get; set; } = null!;
        public string Virtualaccount { get; set; } = null!;
        public string Status { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
