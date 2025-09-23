using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Application.Interfaces.Event;
using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Interfaces.Repositories;
using Infrastructure.Data.EventSourcing;
using Infrastructure.Data.Repositories;
using Infrastructure.MessageBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Presentation.Handlers;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);

builder.Services.AddDbContext<EventStoreDbContext>(options =>
    options.UseNpgsql(connectionString));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentService API", Version = "v1" });
});

var factory = new ConnectionFactory { HostName = builder.Configuration["MessageBus:Host"], DispatchConsumersAsync = true };
builder.Services.AddSingleton(factory.CreateConnection());
builder.Services.AddHostedService<WalletCommandsHandler>();
builder.Services.AddHostedService<PurchaseCommandsHandler>();

builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAWSService<IAmazonSQS>();

builder.Services.Configure<RabbitMQConfig>(builder.Configuration.GetSection("MessageBus"));
builder.Services.AddSingleton<IMessageBusClient, SnsMessageBusClient>(); 
builder.Services.AddHostedService<WalletCommandsHandler>();
builder.Services.AddHostedService<PurchaseCommandsHandler>();

builder.Services.AddScoped<IWalletRepository, EfWalletRepository>();
builder.Services.AddScoped<IPurchaseRepository, EfPurchaseRepository>();

builder.Services.AddScoped<IWalletApplicationService, WalletApplicationService>();
builder.Services.AddScoped<IPurchaseApplicationService, PurchaseApplicationService>();
builder.Services.AddScoped<IEventStoreUnitOfWork, EventStoreUnitOfWork>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentService API V1");
        c.RoutePrefix = "swagger"; 
    });
}

app.UseAuthorization();
app.MapControllers();


app.Run();
