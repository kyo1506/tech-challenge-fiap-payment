using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Mappers;
using Domain.Aggregates;
using Domain.Entities;
using Domain.Entities.Enums;
using Domain.Exceptions;
using Domain.Interfaces.Repositories;
using Marten;
using Microsoft.Win32;
using Shared.DTOs.Commands;
using Shared.DTOs.Events;
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
    IDocumentSession _session,
    IMessageBusClient _messageBus) : IWalletApplicationService
{

    public async Task<TransactionResponse> CreateDepositAsync(CreateDepositCommand command)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);

        if (wallet == null)
        {
            wallet = new UserWallet(command.UserId);
        }

        wallet.Deposit(command.Amount);

        await _walletRepository.StoreAsync(wallet);

        var depositEvent = new FundsDepositedEvent(command.UserId, command.Amount);
        _messageBus.Publish(depositEvent, "wallet.funds.deposited");

        return new TransactionResponse
        {
            TransactionId = Guid.NewGuid(),
            TransactionType = ETransactionType.Deposit.ToString(),
            Amount = command.Amount,
            NewBalance = wallet.Balance
        };
    }


    public async Task<TransactionResponse> CreateWithdrawalAsync(CreateWithdrawalCommand command)
    {

        try
        {
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);
            if (wallet == null)
            {
                throw new NotFoundException($"Wallet for user with ID {command.UserId} not found.");
            }

            wallet.Withdraw(command.Amount);

            await _walletRepository.StoreAsync(wallet);

            await _session.SaveChangesAsync();

            var withdrawalEvent = new FundsWithdrawnEvent(command.UserId, command.Amount);
            _messageBus.Publish(withdrawalEvent, "wallet.funds.withdrawn");

            return new TransactionResponse
            {
                TransactionId = Guid.NewGuid(),
                TransactionType = ETransactionType.Withdrawal.ToString(),
                Amount = -command.Amount, 
                NewBalance = wallet.Balance
            };
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task<BalanceResponse> GetBalanceAsync(Guid userId)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            throw new NotFoundException($"Wallet for user with ID {userId} not found.");
        }

        return wallet.ToBalanceResponse();
    }

    public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(Guid userId)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            throw new NotFoundException($"Wallet for user with ID {userId} not found.");
        }

        var historyEvents = await _walletRepository.GetHistoryAsync(userId);

        return historyEvents.ToHistoryResponse(userId, wallet.Balance);
    }
}
