using System;
using System.Collections.Generic;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using System.Net;
using System.Threading.Tasks;
using OPOService.Models;
using Newtonsoft.Json;

public class KafkaHelper
{
    public static async Task<bool> SendMessage(KafkaSettings settings, string topic, string key, string val)
    {
        var succeed = false;
        var config = new ProducerConfig
        {
            BootstrapServers = settings.Server,
            ClientId = Dns.GetHostName(),

        };
        using (var adminClient = new AdminClientBuilder(config).Build())
        {
            try
            {
                await adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                        new TopicSpecification {
                            Name = topic,
                            NumPartitions = settings.NumPartitions,
                            ReplicationFactor = settings.ReplicationFactor } });
            }
            catch (CreateTopicsException e)
            {
                if (e.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                {
                    Console.WriteLine($"An error occured creating topic {topic}: {e.Results[0].Error.Reason}");
                }
                else
                {
                    Console.WriteLine("Topic already exists");
                }
            }
        }
        using (var producer = new ProducerBuilder<string, string>(config).Build())
        {
            producer.Produce(topic, new Message<string, string>
            {
                Key = key,
                Value = val
            }, (deliveryReport) =>
            {
                if (deliveryReport.Error.Code != ErrorCode.NoError)
                {
                    Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
                }
                else
                {
                    Console.WriteLine($"Produced message to: {deliveryReport.TopicPartitionOffset}");
                    succeed = true;
                }
            });
            producer.Flush(TimeSpan.FromSeconds(10));
        }

        return await Task.FromResult(succeed);
    }
    public static async Task<bool> Consumer()
    {
        var succeed = false;
        var config = new ConsumerConfig
        {
            BootstrapServers = "127.0.0.1:9092",
            GroupId = "tester",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var topic = "OPO";
        //CancellationTokenSource cts = new CancellationTokenSource();
        //Console.CancelKeyPress += (_, e) =>
        //{
        //    e.Cancel = true; // prevent the process from terminating.
        //    cts.Cancel();
        //};

        using (var consumer = new ConsumerBuilder<string, string>(config).Build())
        {
            Console.WriteLine("Connected");
            consumer.Subscribe(topic);
            //try
            //{
                //while (true)
                //{
                    var cr = consumer.Consume(); // blocking
                    var value = cr.Message.Value;
                    Console.WriteLine($"Consumed record with key: {cr.Message.Key} and value: {value}");

                    //// EF
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
                        //newVa.UserId = 3;
                        //newVa.Virtualaccount = m.Virtualaccount;
                        //newVa.Bills = m.Bills;
                        //newVa.PaymentStatus = m.PaymentStatus;
                        user.Bills.Add(newVa);
                        //context.Bills.Add(newVa);
                        context.Users.Update(user);
                        context.SaveChanges();
                    }
                //}
            //}
            //catch (OperationCanceledException)
            //{
            //    // Ctrl-C was pressed.
            //}
            //finally
            //{
                consumer.Close();
            //}
            return await Task.FromResult(succeed);
        }
    }

   


}