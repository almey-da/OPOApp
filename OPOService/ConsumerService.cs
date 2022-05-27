using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OPOService.Models;
using System.Globalization;

namespace OPOService
{
    public class ConsumerService : BackgroundService
    {
        private readonly IConfiguration config;
        private readonly ConsumerConfig _consumerConfig;

        public ConsumerService(IConfiguration configuration)
        {
            config = configuration;
            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = config.GetSection("KafkaSettings").GetValue<string>("Server"),
                GroupId = "tester",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("!!! CONSUMER STARTED !!!\n");

            var task = Task.Run(() => ProcessQueue(stoppingToken), stoppingToken);

            return task;
        }

        private void ProcessQueue(CancellationToken stoppingToken)
        {
            var topic = "OPO";
            using (var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build())
            {
                consumer.Subscribe(topic);
                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var cr = consumer.Consume(stoppingToken);
                        var value = cr.Message.Value;
                        try
                        {
                            Console.WriteLine($"Consumed record with key: {cr.Message.Key} and value: {value}");

                            VirtualAccount m = JsonConvert.DeserializeObject<VirtualAccount>(value);

                            if (m.Virtualaccount == null) throw new Exception("REDEEM");

                            using (var context = new OPOContext())
                            {
                                //string userPhone = m.Virtualaccount.Remove(0, 4);
                                //var user = context.Users.Where(u => u.PhoneNumber == userPhone).FirstOrDefault();
                                if (m.Virtualaccount[..4].Equals("0770"))
                                {
                                    var user = context.Users.Where(o => o.PhoneNumber == m.Virtualaccount.Remove(0, 4))
                                        .Include(o => o.Saldos).Include(o => o.TopUpBanks).FirstOrDefault();

                                    Saldo saldoUser = user.Saldos.FirstOrDefault();
                                    var newSaldo = Convert.ToInt32(m.Bills) + Convert.ToInt32(saldoUser.SaldoUser);

                                    saldoUser.SaldoUser = newSaldo.ToString();

                                    decimal val = Convert.ToDecimal(m.Bills);
                                    string amount = val.ToString("C", CultureInfo.GetCultureInfo("id-ID"));

                                    Transaction newTransaction = new Transaction
                                    {
                                        TransactionName = "TopUp",
                                        TransactionDate = DateTime.Now,
                                        Status = "Completed",
                                        Amount = m.Bills,
                                        Description = $"TopUp {amount} from Bank Success!"
                                    };
                                    user.Transactions.Add(newTransaction);

                                    //var topup = context.TopUpBanks.FirstOrDefault(o => o.Id == m.TransactionId);
                                    TopUpBank topup = user.TopUpBanks.FirstOrDefault(o => o.Id == m.TransactionId);
                                    topup.Status = m.PaymentStatus;

                                    context.Users.Update(user);
                                    //context.Transactions.Add(newTransaction);
                                    //context.TopUpBanks.Update(topup);

                                    //Console.WriteLine($"Update Bill {user.FullName} {saldoUser.SaldoUser}");
                                    Console.WriteLine($"Update TopUpBank");
                                }
                                else
                                {
                                    var newVa = new Bill
                                    {
                                        Virtualaccount = m.Virtualaccount,
                                        Bills = m.Bills,
                                        PaymentStatus = m.PaymentStatus,
                                        TransactionId = m.TransactionId
                                    };
                                    //user.Bills.Add(newVa);
                                    //context.Users.Update(user);
                                    context.Bills.Add(newVa);
                                    Console.WriteLine("Create Bill");
                                }
                                context.SaveChanges();
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            // log
                        }
                        catch (Exception ex2)
                        {
                            if (ex2.Message.ToString().Equals("REDEEM"))
                            {
                                using (var context = new OPOContext())
                                {
                                    RedeemCodeData data = JsonConvert.DeserializeObject<RedeemCodeData>(value);

                                    RedeemCode redeem = new RedeemCode
                                    {
                                        Code = data.Code,
                                        Amount = data.Amount.ToString(),
                                        IsUsed = false
                                    };

                                    context.RedeemCodes.Add(redeem);
                                    context.SaveChanges();
                                    Console.WriteLine("RedeemCode");
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // log
                    consumer.Close();
                }
            }
        }
    }
}
