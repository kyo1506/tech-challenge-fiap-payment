using Domain.Aggregates;
using Domain.Entities;
using JasperFx.Events;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Domain.Events.WalletEvents;

namespace Application.Mappers;

public static class TransactionMapper
{
    public static BalanceResponse ToBalanceResponse(this UserWallet wallet)
    {
        return new BalanceResponse
        {
            UserId = wallet.Id,
            Balance = wallet.Balance
        };
    }

    public static TransactionHistoryResponse ToHistoryResponse(
        this IReadOnlyList<IEvent> events,
        Guid userId,
        decimal currentBalance)
    {
        var historyItems = new List<TransactionHistoryItem>();

        foreach (var e in events)
        {
            switch (e.Data)
            {
                case FundsDeposited fd:
                    historyItems.Add(new TransactionHistoryItem { Type = "Deposit", Amount = fd.Amount, Timestamp = e.Timestamp.UtcDateTime });
                    break;
                case FundsWithdrawn fw:
                    historyItems.Add(new TransactionHistoryItem { Type = "Withdrawal", Amount = -fw.Amount, Timestamp = e.Timestamp.UtcDateTime });
                    break;
                case PurchasePaymentMade ppm:
                    historyItems.Add(new TransactionHistoryItem { Type = "Purchase", Amount = -ppm.Amount, Timestamp = e.Timestamp.UtcDateTime });
                    break;
                case PurchaseRefunded pr:
                    historyItems.Add(new TransactionHistoryItem { Type = "Refund", Amount = pr.Amount, Timestamp = e.Timestamp.UtcDateTime });
                    break;
            }
        }

        return new TransactionHistoryResponse
        {
            UserId = userId,
            CurrentBalance = currentBalance,
            History = historyItems.OrderByDescending(x => x.Timestamp).ToList() 
        };
    }
}
