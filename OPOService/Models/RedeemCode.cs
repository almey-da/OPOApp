using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class RedeemCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Amount { get; set; } = null!;
        public bool IsUsed { get; set; }
    }
}
