using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class Saldo
    {
        public int Id { get; set; }
        public string Saldo1 { get; set; } = null!;
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
