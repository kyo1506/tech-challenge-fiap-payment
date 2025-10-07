using Application.Interfaces.Event;
using Application.Interfaces.MessageBus;
using Application.Interfaces.Services;
using Application.Mappers;
using Domain.Aggregates;
using Domain.Entities;
using Domain.Entities.Enums;
using Domain.Exceptions;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Shared.DTOs.Commands;
using Shared.DTOs.Events;
using Shared.DTOs.Requests;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Application.Services;

public class WalletApplicationService(
    IWalletRepository _walletRepository,
    IEventStoreUnitOfWork _unitOfWork,
    ILogger<WalletApplicationService> _logger,
    ICommandPublisher _commandPublisher) : IWalletApplicationService
{

    public async Task<TransactionResponse> CreateDepositAsync(CreateDepositCommand command)
    {
        _logger.LogInformation("Starting deposit process for User ID: {UserId}, Amount: {Amount}", command.UserId, command.Amount);
        try
        {
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);

            if (wallet == null)
            {
                _logger.LogInformation("Wallet not found for User ID: {UserId}. Creating a new one.", command.UserId);
                wallet = new UserWallet(command.UserId);
            }
            var oldBalance = wallet.Balance;
            wallet.Deposit(command.Amount);

            await _walletRepository.StoreAsync(wallet);
            await _unitOfWork.SaveChangesAsync();

            wallet.ClearUncommittedEvents();

            _logger.LogInformation("Deposit for User ID: {UserId} saved successfully. Balance changed from {OldBalance} to {NewBalance}", command.UserId, oldBalance, wallet.Balance);

            var depositEvent = new FundsDepositedEvent(command.UserId, command.Amount);

            return new TransactionResponse
            {
                TransactionId = Guid.NewGuid(),
                TransactionType = ETransactionType.Deposit.ToString(),
                Amount = command.Amount,
                NewBalance = wallet.Balance
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deposit for User ID: {UserId}", command.UserId);
            throw;
        }
        
    }


    public async Task<TransactionResponse> CreateWithdrawalAsync(CreateWithdrawalCommand command)
    {
        _logger.LogInformation("Starting withdrawal process for User ID: {UserId}, Amount: {Amount}", command.UserId, command.Amount);
        try
        {
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);
            if (wallet == null)
            {
                _logger.LogWarning("Withdrawal failed: Wallet for user with ID {UserId} not found.", command.UserId);
                throw new NotFoundException($"Wallet for user with ID {command.UserId} not found.");
            }
            var oldBalance = wallet.Balance;
            wallet.Withdraw(command.Amount);

            await _walletRepository.StoreAsync(wallet);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Withdrawal for User ID: {UserId} saved successfully. Balance changed from {OldBalance} to {NewBalance}", command.UserId, oldBalance, wallet.Balance);

            wallet.ClearUncommittedEvents();

            var withdrawalEvent = new FundsWithdrawnEvent(command.UserId, command.Amount);

            return new TransactionResponse
            {
                TransactionId = Guid.NewGuid(),
                TransactionType = ETransactionType.Withdrawal.ToString(),
                Amount = -command.Amount, 
                NewBalance = wallet.Balance
            };
        }
        catch (InsufficientBalanceException ex)
        {
            _logger.LogWarning(ex, "Withdrawal failed for User ID {UserId}: Insufficient balance.", command.UserId);
            throw; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process withdrawal for User ID: {UserId}", command.UserId);
            throw;
        }
    }

    public async Task<BalanceResponse> GetBalanceAsync(Guid userId)
    {
        _logger.LogInformation("Fetching balance for User ID: {UserId}", userId);
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            _logger.LogWarning("GetBalance failed: Wallet for user with ID {UserId} not found.", userId);
            throw new NotFoundException($"Wallet for user with ID {userId} not found.");
        }
        _logger.LogInformation("Balance successfully fetched for User ID: {UserId}", userId);
        return wallet.ToBalanceResponse();
    }

    public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(Guid userId)
    {
        _logger.LogInformation("Fetching transaction history for User ID: {UserId}", userId);
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            _logger.LogWarning("GetTransactionHistory failed: Wallet for user with ID {UserId} not found.", userId);
            throw new NotFoundException($"Wallet for user with ID {userId} not found.");
        }

        var historyEvents = await _walletRepository.GetHistoryAsync(userId);

        _logger.LogInformation("Transaction history with {EventCount} events found for User ID: {UserId}", historyEvents.Count, userId);
        return historyEvents.ToHistoryResponse(userId, wallet.Balance);
    }

    public async Task DepositAsync(decimal amount, Guid userId)
    {
        var command = new CreateDepositCommand(userId, amount);
        await _commandPublisher.SendCommandAsync("create-deposit", command, "WalletCommandQueueUrl");
    }

    public async Task WithdrawAsync(decimal amount, Guid userId)
    {
        var command = new CreateWithdrawalCommand(userId, amount);
        await _commandPublisher.SendCommandAsync("create-withdraw", command, "WalletCommandQueueUrl");
    }
}
