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



//==================================================================================================
