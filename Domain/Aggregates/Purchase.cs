using Domain.Aggregates;
using Domain.Entities.Enums;
using Domain.Events;
using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

public class Purchase
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal TotalPrice { get; private set; }
    public EPurchaseStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public List<PurchaseItem> Items { get; private set; } = new();

    public int Version { get; set; }

    private readonly List<object> _uncommittedEvents = new();
    public IEnumerable<object> GetUncommittedEvents() => _uncommittedEvents;

    public Purchase(Guid userId, List<PurchaseItem> items)
    {
        if (items == null || !items.Any())
            throw new DomainException("A purchase must contain at least one item.");

        Raise(new PurchaseCreated(
            Guid.NewGuid(),
            userId,
            items,
            items.Sum(i => i.FinalPrice)
        ));
    }
    public Purchase(IEnumerable<object> history)
    {
        foreach (var @event in history)
        {
            Apply(@event);
        }
    }
    private Purchase() { }

    public void Complete() => Raise(new PurchaseCompleted(Id));
    public void Fail(string reason) => Raise(new PurchaseFailed(Id, reason));
    public void Refund()
    {
        if (Status != EPurchaseStatus.Completed)
            throw new DomainException("Only a completed purchase can be refunded.");
        Raise(new PurchaseRefunded(Id));
    }

    private void Raise(object @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(object @event)
    {
        switch (@event)
        {
            case PurchaseCreated e: Apply(e); break;
            case PurchaseCompleted e: Apply(e); break;
            case PurchaseFailed e: Apply(e); break;
            case PurchaseRefunded e: Apply(e); break;
        }
        Version++;
    }

    private void Apply(PurchaseCreated e)
    {
        Id = e.PurchaseId;
        UserId = e.UserId;
        Items = e.Items;
        TotalPrice = e.TotalPrice;
        Status = EPurchaseStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }
    private void Apply(PurchaseCompleted e) => Status = EPurchaseStatus.Completed;
    private void Apply(PurchaseFailed e) => Status = EPurchaseStatus.Failed;
    private void Apply(PurchaseRefunded e) => Status = EPurchaseStatus.Refunded;
}