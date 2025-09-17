using Application.Interfaces.RabbitMQ;
using Application.Interfaces.Services;
using Application.Mappers;
using Domain.Aggregates;
using Domain.Entities;
using Domain.Entities.Enums;
using Domain.Exceptions;
using Domain.Interfaces.Repositories;
using Marten;
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
    IDocumentSession _session,
    IMessageBusClient _messageBus) : IPurchaseApplicationService
{
    private static readonly Random _random = new Random();
    public async Task<PurchaseResponse> CreatePurchaseAsync(CreatePurchaseCommand command)
    {
        try
        {

            var priceQuote = new ShoppingDTO
            {
                Items = command.GameIds.Select(gameId =>
                {
                    decimal randomPrice = _random.Next(1, 100) + (_random.Next(0, 100) / 100.0m);

                    return new GameDTO
                    {
                        GameId = gameId,
                        GameName = $"Game Title for {gameId}",
                        FinalPrice = Math.Round(randomPrice, 2) 
                    };
                }).ToList()
            };
            if (!priceQuote.Items.Any())
                throw new DomainException("Could not retrieve prices for the requested games.");

            var purchaseItems = priceQuote.Items.Select(item => new PurchaseItem { GameId = item.GameId, Price = item.FinalPrice, GameName = item.GameName }).ToList();
            var purchase = new Purchase(command.UserId, purchaseItems);
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId);
            if (wallet == null) 
                throw new NotFoundException($"Wallet for user {command.UserId} not found.");

            wallet.ProcessPayment(purchase.Id, purchase.TotalPrice);
            purchase.Complete();

            await _purchaseRepository.StoreAsync(purchase);
            await _walletRepository.StoreAsync(wallet);


            await _session.SaveChangesAsync();

            var purchaseCompletedEvent = new PurchaseCompletedEvent(
                purchase.Id,
                purchase.UserId,
                purchase.Items.Select(i => i.GameId).ToList()
            );

            _messageBus.Publish(purchaseCompletedEvent, "purchase.completed");

            return purchase.ToResponse(wallet.Balance);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<RefundResponse> CreateRefundAsync(CreateRefundCommand command)
    {
        try
        {
            var purchase = await _purchaseRepository.GetByIdAsync(command.PurchaseId);
            if (purchase == null) throw new NotFoundException($"Purchase with ID {command.PurchaseId} not found.");

            var wallet = await _walletRepository.GetByUserIdAsync(purchase.UserId);
            if (wallet == null) throw new NotFoundException($"Wallet for user {purchase.UserId} not found.");

            purchase.Refund();
            wallet.ProcessRefund(purchase.Id, purchase.TotalPrice);

            await _purchaseRepository.StoreAsync(purchase);
            await _walletRepository.StoreAsync(wallet);

            await _session.SaveChangesAsync();

            var refundCompletedEvent = new RefundCompletedEvent(
                purchase.Id,
                purchase.UserId,
                purchase.Items.Select(i => i.GameId).ToList()
            );

            _messageBus.Publish(refundCompletedEvent, "purchase.refunded");
            return new RefundResponse
            {
                PurchaseId = purchase.Id,
                RefundAmount = purchase.TotalPrice,
                NewBalance = wallet.Balance
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}

