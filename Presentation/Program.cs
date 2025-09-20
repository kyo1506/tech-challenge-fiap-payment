using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Interfaces.Repositories;
using Infrastructure.Data.Repositories;
using Infrastructure.MessageBus;
using JasperFx;
using Marten;
using Presentation.Handlers;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.AutoCreateSchemaObjects = AutoCreate.All;
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateBootstrapLogger();

var factory = new ConnectionFactory { HostName = builder.Configuration["MessageBus:Host"], DispatchConsumersAsync = true };
builder.Services.AddSingleton(factory.CreateConnection());
builder.Services.AddHostedService<WalletCommandsHandler>();
builder.Services.AddHostedService<PurchaseCommandsHandler>();

builder.Services.Configure<RabbitMQConfig>(builder.Configuration.GetSection("MessageBus"));
builder.Services.AddSingleton<IMessageBusClient, RabbitMQClient>();

builder.Services.AddScoped<IWalletRepository, MartenWalletRepository>();
builder.Services.AddScoped<IPurchaseRepository, MartenPurchaseRepository>();

builder.Services.AddScoped<IWalletApplicationService, WalletApplicationService>();
builder.Services.AddScoped<IPurchaseApplicationService, PurchaseApplicationService>();

builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{

}

app.Run();
