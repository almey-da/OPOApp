using HotChocolate.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OPOService.Models;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OPOService.GraphQL
{
    //REGISTER
    public class Mutation
    {
        public async Task<UserData> RegisterUserAsync(
        RegisterUser input,
        [Service] OPOContext context)
        {
            var user = context.Users.Where(o => o.Username == input.Username || o.PhoneNumber == input.PhoneNumber).FirstOrDefault();
            if (user != null)
            {
                return await Task.FromResult(new UserData());
            }
            var newUser = new User
            {
                FullName = input.FullName,
                Email = input.Email,
                Username = input.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(input.Password),
                PhoneNumber = input.PhoneNumber,
                IsVerified = false,
                IsDeleted = false
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync();
            return await Task.FromResult(new UserData
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                FullName = newUser.FullName,
                PhoneNumber = newUser.PhoneNumber
            });
        }

        //LOGIN
        public async Task<UserToken> LoginAsync(
            LoginUser input,
            [Service] IOptions<TokenSettings> tokenSettings,
            [Service] OPOContext context)
        {
            var user = context.Users.Where(o => o.Username == input.Username && o.IsDeleted == false).FirstOrDefault();
            if (user == null)
            {
                return await Task.FromResult(new UserToken(null, null, "Username or password was invalid"));
            }
            else if (!user.IsVerified)
            {
                return await Task.FromResult(new UserToken(null, null, "Account is not verified"));
            }

            bool valid = BCrypt.Net.BCrypt.Verify(input.Password, user.Password);
            if (valid)
            {
                // generate jwt token
                var securitykey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSettings.Value.Key));
                var credentials = new SigningCredentials(securitykey, SecurityAlgorithms.HmacSha256);

                // jwt payload
                var claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.Name, user.Username));

                var userRoles = context.UserRoles.Where(o => o.UserId == user.Id).ToList(); //diganti userid
                foreach (var userRole in userRoles)
                {
                    var role = context.Roles.Where(o => o.Id == userRole.RoleId).FirstOrDefault();
                    if (role != null)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role.Name));
                    }
                }

                var expired = DateTime.Now.AddHours(15);
                var jwtToken = new JwtSecurityToken(
                    issuer: tokenSettings.Value.Issuer,
                    audience: tokenSettings.Value.Audience,
                    expires: expired,
                    claims: claims, // jwt payload
                    signingCredentials: credentials // signature
                );

                return await Task.FromResult(
                    new UserToken(new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    expired.ToString(), null));
            }
            return await Task.FromResult(new UserToken(null, null, Message: "Username or password was invalid"));
        }

        //VERIFICATION
        [Authorize(Roles = new[] { "ADMIN" })]
        public async Task<string> VerifAsync(
            VerifInput input,
            [Service] OPOContext context)
        {
            var user = context.Users.FirstOrDefault(o => o.Username == input.UserName && o.IsDeleted == false && o.IsVerified == false);
            if (user == null)
            {
                return "User is not found";
            }
            using var transaction = context.Database.BeginTransaction();
            try
            {
                user.IsVerified = true;
                var memberRole = context.Roles.Where(m => m.Name == input.Role).FirstOrDefault();
                if (memberRole == null)
                    throw new Exception("Invalid Role");
                var userRole = new UserRole
                {
                    RoleId = memberRole.Id,
                    UserId = user.Id
                };
                user.UserRoles.Add(userRole);
                if (input.Role == "USER")
                {
                    var saldo = new Saldo
                    {
                        SaldoUser = "0"
                    };
                    user.Saldos.Add(saldo);
                }

                context.Users.Update(user);
                context.SaveChanges();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
            }
            return "Verification success";
        }

        //DELETE USERS
        [Authorize(Roles = new[] { "ADMIN" })]
        public async Task<string> DeleteUserAsync(
            int id,
            [Service] OPOContext context)
        {
            var user = context.Users.FirstOrDefault(o => o.Id == id && o.IsDeleted == false);
            if (user == null)
            {
                return "User is not found";
            }
            user.IsDeleted = true;
            context.Users.Update(user);
            await context.SaveChangesAsync();
            return "Successfully deleted";
        }

        //CHANGE PASS
        [Authorize]
        public async Task<User> ChangePasswordByTokenAsync(
            ChangePassword input, ClaimsPrincipal claimsPrincipal,
            [Service] OPOContext context)
        {
            var username = claimsPrincipal.Identity.Name;
            var user = context.Users.Where(o => o.Username == username).FirstOrDefault();
            bool valid = BCrypt.Net.BCrypt.Verify(input.OldPassword, user.Password);
            if (user != null && valid)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(input.NewPassword);

                context.Users.Update(user);
                await context.SaveChangesAsync();
            }
            return await Task.FromResult(user);
        }

        //TRANSFER AMONG USER
        [Authorize(Roles = new[] { "USER" })]
        public async Task<string> TransferAmongUserAsync(
            TransferInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] OPOContext context)
        {
            var username = claimsPrincipal.Identity.Name;

            var currUser = context.Users.Where(o => o.Username == username && o.IsDeleted == false).Include(o => o.Saldos).FirstOrDefault();
            var targetUser = context.Users.Where(o => o.Username == input.Username || o.PhoneNumber == input.PhoneNumber && o.IsDeleted == false).Include(o => o.Saldos).FirstOrDefault();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                if (currUser == null)
                {
                    return "User is not Found";
                }
                else if (targetUser == null)
                {
                    return "Destination User is not Found!";
                }
                
                Saldo saldoUser = currUser.Saldos.FirstOrDefault();
                Saldo saldoTargetUser = targetUser.Saldos.FirstOrDefault();
                if (Convert.ToInt32(saldoUser.SaldoUser) < Convert.ToInt32(input.Amount))
                {
                    return "The Balance is not Enough!";
                }
                decimal value = Convert.ToDecimal(input.Amount);
                string amount = value.ToString("C", CultureInfo.GetCultureInfo("id-ID"));

                Transaction newTransactionCurrUser = new Transaction
                {
                    TransactionName = "Transfer",
                    TransactionDate = DateTime.Now,
                    Status = "Completed",
                    Amount = input.Amount,
                    Description = $"Transfer {amount} to {targetUser.FullName}"
                };

                Transaction newTransactionTargetUser = new Transaction
                {
                    TransactionName = "Receive",
                    TransactionDate = DateTime.Now,
                    Status = "Completed",
                    Amount = input.Amount,
                    Description = $"Received Balance {amount} from {currUser.FullName}"
                };

                int newSaldoCurrUser = Convert.ToInt32(saldoUser.SaldoUser) - Convert.ToInt32(input.Amount);
                int newSaldoTargetUser = Convert.ToInt32(saldoTargetUser.SaldoUser) + Convert.ToInt32(input.Amount);

                saldoUser.SaldoUser = newSaldoCurrUser.ToString();
                saldoTargetUser.SaldoUser = newSaldoTargetUser.ToString();

                currUser.Transactions.Add(newTransactionCurrUser);
                targetUser.Transactions.Add(newTransactionTargetUser);

                context.Users.Update(currUser);
                context.Users.Update(targetUser);
                context.SaveChanges();
                await transaction.CommitAsync();

                return "Transfer Success!";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return "Transfer Failed!";
            }

        }

        //BILLS
        [Authorize(Roles = new[] { "USER" })]
        public async Task<string> BillsAsync(
          int id, [Service] OPOContext context, ClaimsPrincipal claimsPrincipal, [Service] IOptions<KafkaSettings> settings)
        {
            //Mutation Bill
            var username = claimsPrincipal.Identity.Name;
            var user1 = context.Users.Where(o => o.Username == username).Include(o => o.Saldos).FirstOrDefault();
            using var transaction = context.Database.BeginTransaction();
            try
            {
                Saldo saldoUser = user1.Saldos.FirstOrDefault();
                Bill bill2 = context.Bills.FirstOrDefault(o => o.Id == id && o.PaymentStatus == "Pending");
                //Update Payment Status
                if (bill2 == null)
                {
                    return "Bill Not Found";
                }
                else if (Convert.ToInt32(saldoUser.SaldoUser) < Convert.ToInt32(bill2.Bills))
                {
                    //bill2.PaymentStatus = "Failed";
                    return "The Balance is not Enough";
                }
                else
                {
                    var topic = "";
                    if (bill2.Virtualaccount[..4].Equals("0777")) topic = "SOLAKA";
                    else if (bill2.Virtualaccount[..4].Equals("0778")) topic = "TRAVIKA";

                    bill2.PaymentStatus = "Complete";

                    //Update Saldo User
                    var newSaldoUser = Convert.ToInt32(saldoUser.SaldoUser) - Convert.ToInt32(bill2.Bills);
                    saldoUser.SaldoUser = newSaldoUser.ToString();

                    decimal value = Convert.ToDecimal(bill2.Bills);
                    string amount = value.ToString("C", CultureInfo.GetCultureInfo("id-ID"));

                    Transaction newTransaction = new Transaction
                    {
                        TransactionName = "Payment",
                        TransactionDate = DateTime.Now,
                        Status = "Completed",
                        Amount = bill2.Bills,
                        Description = $"Payment {amount} to {topic}"
                    };
                    user1.Transactions.Add(newTransaction);

                    context.Users.Update(user1);
                    context.Bills.Update(bill2);

                    context.SaveChanges();

                    //await transaction.CommitAsync();

                    var newVA = new VirtualAccount
                    {
                        Bills = bill2.Bills,
                        Virtualaccount = bill2.Virtualaccount,
                        PaymentStatus = bill2.PaymentStatus,
                        TransactionId = bill2.TransactionId
                    };

                    var dts = DateTime.Now.ToString();
                    var key = "order-" + dts;
                    var val = JsonConvert.SerializeObject(newVA);

                    var result = await KafkaHelper.SendMessage(settings.Value, topic , key, val);

                    if (result)
                    {
                        await transaction.CommitAsync();
                        return "Tagihan Berhasil dibayar";
                    }
                    else
                    {
                        transaction.Rollback();
                        return "Tagihan Gagal dibayar";
                    }
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return "Gagal membayar tagihan";
            }
        }

        //REDEEM CODE
        [Authorize(Roles = new[] { "USER" })]
        public async Task<string> RedeemCodeAsync(
            string code, [Service] OPOContext context, ClaimsPrincipal claimsPrincipal)
        {
            var redeemCode = context.RedeemCodes.Where(o => o.Code == code && o.IsUsed == false).FirstOrDefault();
            var userName = claimsPrincipal.Identity.Name;
            var user = context.Users.Where(u => u.Username == userName).Include(s=>s.Saldos).FirstOrDefault();

            if (redeemCode == null)
                return "The RedeemCode is not Found";
            if (user != null)
            {
                using var transaction = context.Database.BeginTransaction();
                try
                {
                    decimal value = Convert.ToDecimal(redeemCode.Amount);
                    string amount = value.ToString("C", CultureInfo.GetCultureInfo("id-ID"));

                    Transaction newTransaction = new Transaction
                    {
                        TransactionName = "TopUp",
                        TransactionDate = DateTime.Now,
                        Status = "Completed",
                        Amount = redeemCode.Amount,
                        Description = $"TopUp {amount} from Bank through RedeemCode"
                    };
                    user.Transactions.Add(newTransaction);
                    Saldo saldo = user.Saldos.FirstOrDefault();
                    int newsaldo = Int32.Parse(saldo.SaldoUser) + Int32.Parse(redeemCode.Amount);
                    saldo.SaldoUser = newsaldo.ToString();
                    //context.Saldos.Update(saldo);
                    context.Users.Update(user);
                    redeemCode.IsUsed = true;

                    context.RedeemCodes.Update(redeemCode);
                    context.SaveChanges();
                    await transaction.CommitAsync();
                    return "TopUp Success!";
                }
                catch
                {
                    transaction.Rollback();
                    return "TopUp Failed!";
                }
            }
            else return "User is not Found!";
        }

        //TOPUP BANK
        [Authorize(Roles = new[] { "USER" })]
        public async Task<string> TopUpBankAsync(
            string amount, ClaimsPrincipal claimsPrincipal, [Service] IOptions<KafkaSettings> settings,
            [Service] OPOContext context)
        {
            var username = claimsPrincipal.Identity.Name;
            var user = context.Users.Where(o => o.Username == username).Include(o => o.Saldos).FirstOrDefault();

            if (user != null)
            {
                TopUpBank topUpBank = new TopUpBank
                {
                    Amount = amount,
                    Virtualaccount = $"0770{user.PhoneNumber}",
                    Status = "Pending"
                };

                user.TopUpBanks.Add(topUpBank);
                context.Users.Update(user);

                var newVA = new VirtualAccount
                {
                    Bills = amount,
                    Virtualaccount = $"0770{user.PhoneNumber}",
                    PaymentStatus = "Pending",
                    TransactionId = topUpBank.Id
                };

                var dts = DateTime.Now.ToString();
                var key = "order-" + dts;
                var val = JsonConvert.SerializeObject(newVA);

                var result = await KafkaHelper.SendMessage(settings.Value, "Bank", key, val);

                if (result)
                {
                    await context.SaveChangesAsync();
                    return "Please Pay through Bank";
                }
                else
                {
                    return "TopUp is Failed";
                }
            }
            else return "User is not Found!";
        }
    }
}