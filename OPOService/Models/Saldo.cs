using System;
using System.Collections.Generic;

namespace OPOService.Models
{
    public partial class Saldo
    {
        public int Id { get; set; }
        public string SaldoUser { get; set; } = null!;
        public int UserId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
