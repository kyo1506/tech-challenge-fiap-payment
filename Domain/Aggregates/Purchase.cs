using Domain.Aggregates;
using Domain.Entities.Enums;
using Domain.Events;
using Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

// Assumindo que este arquivo está em 'Domain/Aggregates/Purchase.cs',
// o namespace deveria ser Domain.Aggregates.
// Se não estiver, adicione 'namespace Domain.Aggregates;' envolvendo a classe.
public class Purchase
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal TotalPrice { get; private set; }
    public EPurchaseStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public List<PurchaseItem> Items { get; private set; } = new();

    // A versão não precisa mais ser pública
    public int Version { get; set; }

    private readonly List<object> _uncommittedEvents = new();
    public IEnumerable<object> GetUncommittedEvents() => _uncommittedEvents;

    // Construtor para criar uma NOVA compra
    public Purchase(Guid userId, List<PurchaseItem> items)
    {
        if (items == null || !items.Any())
            throw new DomainException("A purchase must contain at least one item.");

        Raise(new PurchaseCreated(
            Guid.NewGuid(),
            userId,
            items,
            items.Sum(i => i.Price)
        ));
    }

    // Construtor vazio para o Marten
    private Purchase() { }

    // --- MÉTODOS DE NEGÓCIO ---
    public void Complete() => Raise(new PurchaseCompleted(Id));
    public void Fail(string reason) => Raise(new PurchaseFailed(Id, reason));
    public void Refund()
    {
        if (Status != EPurchaseStatus.Completed)
            throw new DomainException("Only a completed purchase can be refunded.");
        Raise(new PurchaseRefunded(Id));
    }

    // --- LÓGICA INTERNA DE EVENT SOURCING ---
    private void Raise(object @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    // O método Apply agora é privado e sobrecarregado.
    // O Marten é inteligente o suficiente para encontrar e chamar o método correto para cada evento.
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

    // Métodos Apply específicos para cada evento
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