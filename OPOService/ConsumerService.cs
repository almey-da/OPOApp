using Confluent.Kafka;
using Newtonsoft.Json;
using OPOService.Models;

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
                        try
                        {
                            var cr = consumer.Consume(stoppingToken);
                            var value = cr.Message.Value;
                            Console.WriteLine($"Consumed record with key: {cr.Message.Key} and value: {value}");

                            VirtualAccount m = JsonConvert.DeserializeObject<VirtualAccount>(value);
                            using (var context = new OPOContext())
                            {
                                string userPhone = m.Virtualaccount.Remove(0, 3);
                                var user = context.Users.Where(u => u.PhoneNumber == userPhone).FirstOrDefault();
                                var newVa = new Bill
                                {
                                    Virtualaccount = m.Virtualaccount,
                                    Bills = m.Bills,
                                    PaymentStatus = m.PaymentStatus,
                                    TransactionId = m.TransactionId
                                };
                                user.Bills.Add(newVa);
                                context.Users.Update(user);
                                context.SaveChanges();
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            // log
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
