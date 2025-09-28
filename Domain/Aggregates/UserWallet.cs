using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain.Events.WalletEvents;

namespace Domain.Aggregates;

public class UserWallet
{
    public Guid Id { get; private set; }
    public decimal Balance { get; private set; }
    public long Version { get; private set; }

    private readonly List<object> _uncommittedEvents = new();
    public IEnumerable<object> GetUncommittedEvents() => _uncommittedEvents;

    public UserWallet(Guid userId) => Raise(new WalletCreated(userId));
    public UserWallet(IEnumerable<object> history)
    {
        foreach (var @event in history)
        {
            Apply(@event);
        }
    }
    private UserWallet() { }

    private void Raise(object @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(object @event)
    {
        Version++;
        switch (@event)
        {
            case WalletCreated e: Id = e.UserId; Balance = 0; break;
            case FundsDeposited e: Balance += e.Amount; break;
            case FundsWithdrawn e: Balance -= e.Amount; break;
            case PurchasePaymentMade e: Balance -= e.Amount; break;
            case WalletCreditedForPurchaseRefund e: Balance += e.Amount; break;
        }
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("The deposit amount must be positive.");

        Raise(new FundsDeposited(Id, amount));
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("The withdrawal amount must be positive.");

        if (Balance < amount)
            throw new InsufficientBalanceException(Balance, amount);

        Raise(new FundsWithdrawn(Id, amount));
    }

    public void ProcessRefund(Guid purchaseId, decimal amountToCredit)
    {
        if (amountToCredit <= 0)
            throw new DomainException("Refund amount must be positive.");

        Raise(new WalletCreditedForPurchaseRefund(Id, purchaseId, amountToCredit));
    }

    public void ProcessPayment(Guid purchaseId, decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        if (Balance < amount)
            throw new InsufficientBalanceException(Balance, amount);

        Raise(new PurchasePaymentMade(Id, purchaseId, amount));
    }
}
