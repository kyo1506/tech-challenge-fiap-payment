using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Application.Interfaces.Event;
using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Interfaces.Repositories;
using Infrastructure.Data.EventSourcing;
using Infrastructure.Data.Repositories;
using Infrastructure.Logging;
using Infrastructure.MessageBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Presentation.Middleware;
using Serilog;
using Serilog.Enrichers.CorrelationId;
using Serilog.Sinks.Elasticsearch;
using System.Collections.Specialized;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var awsOptions = builder.Configuration.GetAWSOptions();
builder.Services.AddDefaultAWSOptions(awsOptions);

builder.Services.AddDbContext<EventStoreDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "PaymentService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddAWSInstrumentation()    
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri("http://localhost:4317");
        }));


Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("X-Request-ID", context.HostingEnvironment.ApplicationName)
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(context.Configuration["Elasticsearch:Uri"]))
    {
        IndexFormat = "fcg-logs-{0:yyyy.MM.dd}",
        TypeName = null,
        AutoRegisterTemplate = true,
        OverwriteTemplate = true,
        NumberOfShards = 1,
        NumberOfReplicas = 1,
        ModifyConnectionSettings = x => x.ApiKeyAuthentication(context.Configuration["Elasticsearch:Id"], context.Configuration["Elasticsearch:ApiKey"])
    })
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentService API", Version = "v1" });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAWSService<IAmazonSQS>();

builder.Services.AddScoped<IWalletRepository, EfWalletRepository>();
builder.Services.AddScoped<IPurchaseRepository, EfPurchaseRepository>();

builder.Services.AddScoped<IWalletApplicationService, WalletApplicationService>();
builder.Services.AddScoped<IPurchaseApplicationService, PurchaseApplicationService>();
builder.Services.AddScoped<IEventStoreUnitOfWork, EventStoreUnitOfWork>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

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

app.UseHttpsRedirection();

app.UseCors("AllowAllOrigins");
app.UseMiddleware<JwtMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
