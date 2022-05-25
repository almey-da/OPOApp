using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OPOService.GraphQL;
using OPOService.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var conString = builder.Configuration.GetConnectionString("MyDatabase");
builder.Services.AddDbContext<OPOContext>(options =>
     options.UseSqlServer(conString)
);

// graphql
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddAuthorization();

builder.Services.AddControllers();
// DI Dependency Injection
builder.Services.Configure<TokenSettings>(builder.Configuration.GetSection("TokenSettings"));

//kafka
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaSettings"));

// role-based identity
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration.GetSection("TokenSettings").GetValue<string>("Issuer"),
            ValidateIssuer = true,
            ValidAudience = builder.Configuration.GetSection("TokenSettings").GetValue<string>("Audience"),
            ValidateAudience = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("TokenSettings").GetValue<string>("Key"))),
            ValidateIssuerSigningKey = true
        };

    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("allowedOrigin", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
var app = builder.Build();



app.UseAuthentication();
app.UseAuthorization();
app.MapGraphQL();
app.MapGet("/", () => "Hello World!");

app.Run();

//===========================================================Kafka================================

var config = new ConsumerConfig
{
    BootstrapServers = "127.0.0.1:9092",
    GroupId = "tester",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

var topic = "OPO";
CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent the process from terminating.
    cts.Cancel();
};

using (var consumer = new ConsumerBuilder<string, string>(config).Build())
{
    Console.WriteLine("Connected");
    consumer.Subscribe(topic);
    try
    {
        while (true)
        {
            var cr = consumer.Consume(cts.Token); // blocking
            var value = cr.Message.Value;
            Console.WriteLine($"Consumed record with key: {cr.Message.Key} and value: {value}");

            //// EF
            VirtualAccount m = JsonConvert.DeserializeObject<VirtualAccount>(value);
            using (var context = new OPOContext())
            {
                string userPhone = m.Virtualaccount.Remove(0, 4);
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
        }
    }
    catch (OperationCanceledException)
    {
        // Ctrl-C was pressed.
    }
    finally
    {
        consumer.Close();
    }
}



//==================================================================================================
