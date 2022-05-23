using HotChocolate.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OPOService.Models;
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
            var user = context.Users.Where(o => o.Username == input.Username).FirstOrDefault();
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
                return await Task.FromResult(new UserToken(null, null, "Account not verified"));
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
                return "User not found";
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
                if(input.Role == "USER")
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
                return "User not found";
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

        [Authorize(Roles = new[] { "USER" })]
        public async Task<string> TopUpByTokenAsync(
            string amount, ClaimsPrincipal claimsPrincipal,
            [Service] OPOContext context)
        {
            var username = claimsPrincipal.Identity.Name;
            var user = context.Users.Where(o => o.Username == username).Include(o => o.Saldos).FirstOrDefault();
            //bool valid = BCrypt.Net.BCrypt.Verify(input.OldPassword, user.Password);
            if (user != null)
            {
                var transaction = context.Database.BeginTransaction();
                try
                {
                    Transaction newTransaction = new Transaction
                    {
                        TransactionName = "TopUp",
                        TransactionDate = DateTime.Now,
                        Status = "Completed",
                        Amount = amount,
                        Description = $"TopUp sebersar Rp{amount} dari Bank..."
                    };
                    user.Transactions.Add(newTransaction);
                    Saldo saldo = user.Saldos.FirstOrDefault();
                    int newsaldo = Int32.Parse(saldo.SaldoUser) + Int32.Parse(amount);
                    saldo.SaldoUser = newsaldo.ToString();
                    context.Users.Update(user);
                    context.SaveChanges();
                    await transaction.CommitAsync();
                    return "TopUp Berhasil!";
                }
                catch
                {
                    transaction.Rollback();
                    return "TopUp Gagal!";
                }
            }
            else return "User Tidak Ada!";
        }

    }
}
