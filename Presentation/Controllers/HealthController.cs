using Microsoft.AspNetCore.Mvc;
using Infrastructure.Data.EventSourcing;
using Microsoft.EntityFrameworkCore;

namespace Presentation.Controllers;

[ApiController]
[Route("health")]
public class HealthController(
    EventStoreDbContext _dbContext,
    ILogger<HealthController> _logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            // Test RDS database connection
            await _dbContext.Database.CanConnectAsync();
            
            _logger.LogInformation("Health check passed - RDS connection successful");
            
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                service = "PaymentService",
                version = "1.0.0",
                database = "RDS Connected",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed - RDS connection error");
            
            return StatusCode(503, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow,
                service = "PaymentService",
                version = "1.0.0",
                database = "RDS Disconnected",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                error = ex.Message
            });
        }
    }
}