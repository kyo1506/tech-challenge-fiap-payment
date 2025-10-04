using Domain.Aggregates;
using Domain.Exceptions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Domain.Events.WalletEvents;

namespace Domain.Tests.Aggregates;

public class UserWalletTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void CreateNewWallet()
    {
        var wallet = new UserWallet(_userId);

        wallet.Balance.Should().Be(0);

        wallet.Id.Should().Be(_userId);

        var events = wallet.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(WalletCreated));
    }

    [Fact]
    public void Deposit_WithPositiveAmount()
    {
        var wallet = new UserWallet(_userId);
        var depositAmount = 100.50m;

        wallet.Deposit(depositAmount);

        wallet.Balance.Should().Be(depositAmount);

        var events = wallet.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(FundsDeposited))
              .Which.Should().BeEquivalentTo(new FundsDeposited(wallet.Id, depositAmount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Deposit_WithNonPositiveAmount(decimal invalidAmount)
    {
        var wallet = new UserWallet(_userId);

        Action act = () => wallet.Deposit(invalidAmount);

        act.Should().Throw<DomainException>()
           .WithMessage("The deposit amount must be positive.");
    }

    [Fact]
    public void Withdraw_WithInsufficientBalance()
    {
        var wallet = new UserWallet(_userId); 

        Action act = () => wallet.Withdraw(50);

        act.Should().Throw<InsufficientBalanceException>();
    }

    [Fact]
    public void Withdraw_WithSufficientBalance()
    {
        var wallet = new UserWallet(_userId);
        wallet.Deposit(100); 
        wallet.ClearUncommittedEvents(); 

        var withdrawAmount = 75.50m;

        wallet.Withdraw(withdrawAmount);

        wallet.Balance.Should().Be(24.50m);

        var events = wallet.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(FundsWithdrawn))
              .Which.Should().BeEquivalentTo(new FundsWithdrawn(wallet.Id, withdrawAmount));
    }


    [Fact]
    public void ProcessPayment_WithSufficientBalance()
    {
        var wallet = new UserWallet(_userId);
        wallet.Deposit(200); 
        wallet.ClearUncommittedEvents(); 
        var purchaseId = Guid.NewGuid();
        var paymentAmount = 150;

        wallet.ProcessPayment(purchaseId, paymentAmount);

        wallet.Balance.Should().Be(50); 

        var events = wallet.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(PurchasePaymentMade))
              .Which.Should().BeEquivalentTo(new PurchasePaymentMade(wallet.Id, purchaseId, paymentAmount));
    }

    [Fact]
    public void ProcessPayment_WithInsufficientBalance_ShouldThrowInsufficientBalanceException()
    {
        var wallet = new UserWallet(_userId);
        wallet.Deposit(100); 
        var paymentAmount = 150;

        Action act = () => wallet.ProcessPayment(Guid.NewGuid(), paymentAmount);

        act.Should().Throw<InsufficientBalanceException>();
    }

    [Fact]
    public void ProcessRefund_ShouldIncreaseBalanceAndRaiseEvent()
    {
        var wallet = new UserWallet(_userId); 
        wallet.ClearUncommittedEvents();
        var purchaseId = Guid.NewGuid();
        var refundAmount = 199.99m;

        wallet.ProcessRefund(purchaseId, refundAmount);

        wallet.Balance.Should().Be(refundAmount);

        var events = wallet.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(WalletCreditedForPurchaseRefund))
              .Which.Should().BeEquivalentTo(new WalletCreditedForPurchaseRefund(wallet.Id, purchaseId, refundAmount));
    }
}
