using Domain.Aggregates;
using Domain.Entities.Enums;
using Domain.Events;
using Domain.Exceptions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Tests.Aggregates;

public class PurchaseTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private List<PurchaseItem> CreateSampleItems() =>
        new List<PurchaseItem>
        {
                new PurchaseItem { GameId = Guid.NewGuid(), OriginalPrice = 100, DiscountPercentage = 10 }, 
                new PurchaseItem { GameId = Guid.NewGuid(), OriginalPrice = 50.50m, DiscountPercentage = null } 
        };

    [Fact]
    public void CreateNewPurchase()
    {
        var items = CreateSampleItems();

        var purchase = new Purchase(_userId, items);

        purchase.TotalPrice.Should().Be(140.50m); 
        purchase.Status.Should().Be(EPurchaseStatus.Pending);
        purchase.UserId.Should().Be(_userId);

        var events = purchase.GetUncommittedEvents().ToList();
        events.Should().ContainSingle(e => e.GetType() == typeof(PurchaseCreated));
    }

    [Fact]
    public void CreateNewPurchase_WithEmptyItems()
    {
        var emptyItems = new List<PurchaseItem>();

        Action act = () => new Purchase(_userId, emptyItems);

        act.Should().Throw<DomainException>()
           .WithMessage("A purchase must contain at least one item.");
    }

    [Fact]
    public void Complete_OnPendingPurchase()
    {
        var purchase = new Purchase(_userId, CreateSampleItems());
        purchase.ClearUncommittedEvents();

        purchase.Complete();

        purchase.Status.Should().Be(EPurchaseStatus.Completed);
        purchase.GetUncommittedEvents().Should().ContainSingle(e => e.GetType() == typeof(PurchaseCompleted));
    }

    [Fact]
    public void Refund_OnCompletedPurchase()
    {
        var purchase = new Purchase(_userId, CreateSampleItems());
        purchase.Complete(); 
        purchase.ClearUncommittedEvents();

        purchase.Refund();

        purchase.Status.Should().Be(EPurchaseStatus.Refunded);
        purchase.GetUncommittedEvents().Should().ContainSingle(e => e.GetType() == typeof(PurchaseRefunded));
    }

    [Theory]
    [InlineData(EPurchaseStatus.Pending)]
    [InlineData(EPurchaseStatus.Refunded)]
    [InlineData(EPurchaseStatus.Failed)]
    public void Refund_OnNonCompletedPurchase(EPurchaseStatus initialStatus)
    {
        var events = new List<object>
            {
                new PurchaseCreated(Guid.NewGuid(), _userId, CreateSampleItems(), 140.50m)
            };

        if (initialStatus == EPurchaseStatus.Completed) events.Add(new PurchaseCompleted(Guid.NewGuid()));
        if (initialStatus == EPurchaseStatus.Refunded)
        {
            events.Add(new PurchaseCompleted(Guid.NewGuid()));
            events.Add(new PurchaseRefunded(Guid.NewGuid()));
        }
        if (initialStatus == EPurchaseStatus.Failed) events.Add(new PurchaseFailed(Guid.NewGuid(), "reason"));

        var purchase = new Purchase(events);
        purchase.ClearUncommittedEvents();

        Action act = () => purchase.Refund();

        if (initialStatus != EPurchaseStatus.Completed)
        {
            act.Should().Throw<DomainException>()
               .WithMessage("Only a completed purchase can be refunded.");
        }
        else
        {
            act.Should().NotThrow(); 
        }
    }
}
