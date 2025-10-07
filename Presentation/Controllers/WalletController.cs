using Application.Interfaces.Services;
using Application.Services;
using Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;
using Shared.DTOs.Requests;
using Shared.DTOs.Responses;

namespace Presentation.Controllers;

[ApiController]
[Route("v1/wallet")]
public class WalletController(
    IWalletApplicationService _walletService,
    ILogger<WalletController> _logger) : ControllerBase
{
    [HttpGet("{userId}/balance")]
    public async Task<ActionResult<BalanceResponse>> GetBalance([FromRoute] Guid userId)
    {
        try
        {
            if (!HttpContext.IsAuthenticated())
            {
                _logger.LogWarning("Tentativa de acesso sem JWT válido");
                return Unauthorized(
                    new { Message = "Token JWT válido é obrigatório para gerenciar usuários" }
                );
            }

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
            if (!HttpContext.IsAuthenticated())
            {
                _logger.LogWarning("Tentativa de acesso sem JWT válido");
                return Unauthorized(
                    new { Message = "Token JWT válido é obrigatório para gerenciar usuários" }
                );
            }

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

    [HttpPost("deposit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest depositRequest)
    {
        string userIdStr = String.Empty;
        try
        {
            if (!HttpContext.IsAuthenticated())
            {
                _logger.LogWarning("Tentativa de depósito sem JWT válido");
                return Unauthorized(new { Message = "Token JWT válido é obrigatório." });
            }

            if (depositRequest.Amount <= 0)
            {
                return BadRequest("O valor deve ser maior que zero.");
            }

            userIdStr = HttpContext.GetUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return BadRequest("UserId inválido no token.");
            }

            await _walletService.DepositAsync(depositRequest.Amount, userId);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while depositing for User ID: {UserId}", userIdStr);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
        
    }

    [HttpPost("withdraw")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest withdrawRequest)
    {
        string userIdStr = String.Empty;
        try
        {
            if (!HttpContext.IsAuthenticated())
            {
                _logger.LogWarning("Tentativa de saque sem JWT válido");
                return Unauthorized(new { Message = "Token JWT válido é obrigatório." });
            }

            if (withdrawRequest.Amount <= 0)
            {
                return BadRequest("O valor deve ser maior que zero.");
            }

            userIdStr = HttpContext.GetUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return BadRequest("UserId inválido no token.");
            }

            await _walletService.WithdrawAsync(withdrawRequest.Amount, userId);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while withdrawing for User ID: {UserId}", userIdStr);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
        
    }
}
