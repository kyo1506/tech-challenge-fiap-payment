using Application.Interfaces.Services;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Responses;

namespace Presentation.Controllers;

[ApiController]
[Route("api/wallet")]
public class WalletController(
    IWalletApplicationService _walletService,
    ILogger<WalletController> _logger) : ControllerBase
{
    [HttpGet("{userId}/balance")]
    public async Task<ActionResult<BalanceResponse>> GetBalance([FromRoute] Guid userId)
    {
        try
        {
            var response = await _walletService.GetBalanceAsync(userId);
            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while fetching balance for User ID: {UserId}", userId);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    [HttpGet("{userId}/history")]
    public async Task<IActionResult> GetHistory([FromRoute] Guid userId)
    {
        try
        {
            var response = await _walletService.GetTransactionHistoryAsync(userId);
            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while fetching history for User ID: {UserId}", userId);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
}
