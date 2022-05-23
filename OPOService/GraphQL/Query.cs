using HotChocolate.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OPOService.Models;
using System.Security.Claims;

namespace OPOService.GraphQL
{
    public class Query
    {
        //GET ALL USERS
        [Authorize(Roles = new[] { "ADMIN" })]
        public IQueryable<User> GetUsers([Service] OPOContext context) =>
            context.Users.Where(o => o.IsDeleted == false).Include(o => o.Saldos);

        //GET SALDO
        [Authorize(Roles = new[] { "USER" })]
        public IQueryable<Saldo> GetSaldoByToken([Service] OPOContext context, ClaimsPrincipal claimsPrincipal)
        {
            var userName = claimsPrincipal.Identity.Name;
            var user = context.Users.FirstOrDefault(o => o.Username == userName);
            Saldo saldo = context.Saldos.FirstOrDefault(o => o.UserId == user.Id);
            List<Saldo> saldos = new();
            saldos.Add(saldo);

            return saldos.AsQueryable();
        }
        [Authorize(Roles = new[] { "USER" })]
        public IQueryable<Transaction> GetTransactionByToken([Service] OPOContext context, ClaimsPrincipal claimsPrincipal)
        {
            var userName = claimsPrincipal.Identity.Name;
            var user = context.Users.FirstOrDefault(o => o.Username == userName);
            var transaction = context.Transactions.Where(o => o.UserId == user.Id).ToList();
            
            return transaction.AsQueryable();
        }
    }
}