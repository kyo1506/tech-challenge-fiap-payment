using Application.Interfaces.Event;
using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Mappers;
using Domain.Aggregates;
using Domain.Entities;
using Domain.Entities.Enums;
using Domain.Exceptions;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Shared.DTOs;
using Shared.DTOs.Commands;
using Shared.DTOs.Events;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services;

public class PurchaseApplicationService(
    IPurchaseRepository _purchaseRepository,
    IWalletRepository _walletRepository,
    IEventStoreUnitOfWork _unitOfWork,
    ILogger<PurchaseApplicationService> _logger) : IPurchaseApplicationService
{
    public async Task<PurchaseResponse> CreatePurchaseAsync(CreatePurchaseCommand command)
    {
        try
        {
            _logger.LogInformation("Starting purchase process for User ID: {UserId} with {GameCount} games.", command.UserId, command.Games.Count);

            var purchaseItems = command.Games.Select(itemCmd => new PurchaseItem
            {
                GameId = itemCmd.GameId,
                OriginalPrice = itemCmd.Price,
                DiscountPercentage = itemCmd.Discount,
                PromotionId = itemCmd.PromotionId
            }).ToList();

            var purchase = new Purchase(command.UserId, purchaseItems);
            _logger.LogInformation("New Purchase aggregate created with ID: {PurchaseId}", purchase.Id);
            
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);
            if (wallet == null)
            {
                _logger.LogWarning("Purchase failed: Wallet for User ID {UserId} not found.", command.UserId);
                throw new NotFoundException($"Wallet for user {command.UserId} not found.");
            }

            wallet.ProcessPayment(purchase.Id, purchase.TotalPrice);
            purchase.Complete();
            _logger.LogInformation("Domain logic applied for Purchase ID: {PurchaseId}. New wallet balance (in memory): {NewBalance}", purchase.Id, wallet.Balance);

            await _purchaseRepository.StoreAsync(purchase);
            await _walletRepository.StoreAsync(wallet);

            _logger.LogInformation("Purchase {PurchaseId} and Wallet {WalletId} state successfully saved to database.", purchase.Id, wallet.Id);

            await _unitOfWork.SaveChangesAsync();
            purchase.ClearUncommittedEvents();
            wallet.ClearUncommittedEvents();
            var response = new PurchaseResponse
            {
                UserId = purchase.UserId,
                PaymentTransactionId = purchase.Id,
                Games = purchase.Items.Select(item =>
                {
                    var originalItemCmd = command.Games.FirstOrDefault(c => c.GameId == item.GameId);
                    return new PurchaseItemResponse
                    {
                        GameId = item.GameId,
                        Price = item.OriginalPrice,
                        Discount = item.DiscountPercentage,
                        PromotionId = item.PromotionId,
                        HistoryPaymentId = originalItemCmd?.HistoryPaymentId ?? Guid.Empty
                    };
                }).ToList()
            };
            
            _logger.LogInformation("PurchaseCompletedEvent published for Purchase ID: {PurchaseId}", purchase.Id);

            return response;
        }
        catch (InsufficientBalanceException ex)
        {
            _logger.LogWarning(ex, "Purchase failed for User ID {UserId}: Insufficient balance.", command.UserId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during purchase process for User ID: {UserId}", command.UserId);
            throw;
        }
    }

    public async Task<RefundResponse> CreateRefundAsync(CreateRefundCommand command)
    {
        try
        {
            _logger.LogInformation("Starting refund process for Purchase ID: {PurchaseId}", command.PurchaseId);

            var purchase = await _purchaseRepository.GetByIdAsync(command.PurchaseId);
            if (purchase == null)
            {
                _logger.LogWarning("Refund failed: Purchase with ID {PurchaseId} not found.", command.PurchaseId);
                throw new NotFoundException($"Purchase with ID {command.PurchaseId} not found.");
            }

            var wallet = await _walletRepository.GetByUserIdAsync(purchase.UserId);
            if (wallet == null)
            {
                _logger.LogWarning("Refund failed: Wallet for User ID {UserId} associated with Purchase ID {PurchaseId} not found.", purchase.UserId, command.PurchaseId);
                throw new NotFoundException($"Wallet for user {purchase.UserId} not found.");
            }

            purchase.Refund();
            wallet.ProcessRefund(purchase.Id, purchase.TotalPrice);
            _logger.LogInformation("Domain logic applied for refund on Purchase ID: {PurchaseId}. New wallet balance (in memory): {NewBalance}", purchase.Id, wallet.Balance);

            await _purchaseRepository.StoreAsync(purchase);
            await _walletRepository.StoreAsync(wallet);

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Refund for Purchase {PurchaseId} and Wallet {WalletId} state successfully saved to database.", purchase.Id, wallet.Id);

            purchase.ClearUncommittedEvents();
            wallet.ClearUncommittedEvents();
            var refundCompletedEvent = new RefundCompletedEvent(
                purchase.Id,
                purchase.UserId,
                purchase.Items.Select(i => i.GameId).ToList()
            );

            _logger.LogInformation("RefundCompletedEvent published for Purchase ID: {PurchaseId}", purchase.Id);
            
            return new RefundResponse
            {
                PurchaseId = purchase.Id,
                RefundAmount = purchase.TotalPrice,
                NewBalance = wallet.Balance
            };
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Refund failed for Purchase ID {PurchaseId} due to a business rule violation.", command.PurchaseId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during refund process for Purchase ID: {PurchaseId}", command.PurchaseId);
            throw;
        }
    }
}

