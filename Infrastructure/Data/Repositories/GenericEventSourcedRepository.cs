using Domain.Events;
using Infrastructure.Data.EventSourcing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Domain.Events.WalletEvents;

namespace Infrastructure.Data.Repositories;

public abstract class GenericEventSourcedRepository<T> where T : class
{
    protected readonly EventStoreDbContext _context;
    private readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

    protected GenericEventSourcedRepository(EventStoreDbContext context)
    {
        _context = context;
    }

    protected async Task<T?> LoadAsync(Guid streamId)
    {
        var storedEvents = await _context.Events
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        if (!storedEvents.Any())
        {
            return null;
        }

        var domainEvents = storedEvents.Select(se =>
        {

            Type eventType = se.EventType switch
            {
                nameof(WalletCreated) => typeof(WalletCreated),
                nameof(FundsDeposited) => typeof(FundsDeposited),
                nameof(FundsWithdrawn) => typeof(FundsWithdrawn),
                nameof(PurchasePaymentMade) => typeof(PurchasePaymentMade),
                nameof(WalletCreditedForPurchaseRefund) => typeof(WalletCreditedForPurchaseRefund),
                nameof(PurchaseCreated) => typeof(PurchaseCreated),
                nameof(PurchaseCompleted) => typeof(PurchaseCompleted),
                nameof(PurchaseRefunded) => typeof(PurchaseRefunded),
                _ => throw new NotSupportedException($"Event type '{se.EventType}' is not supported.")
            };
            return JsonSerializer.Deserialize(se.Data, eventType, _serializerOptions);
        }).ToList();

        var aggregate = (T)Activator.CreateInstance(typeof(T), new object[] { domainEvents });

        return aggregate;
    }

    protected async Task StoreAsync(dynamic aggregate)
    {
        try
        {
            var events = ((IEnumerable<object>)aggregate.GetUncommittedEvents()).ToArray();
            if (!events.Any())
                return;

            var streamId = (Guid)aggregate.Id;
            var currentVersion = (int)aggregate.Version;
            var startingVersion = currentVersion - events.Length;


            foreach (var @event in events)
            {
                startingVersion++;
                var storedEvent = new StoredEvent
                {
                    Id = Guid.NewGuid(),
                    StreamId = streamId,
                    Version = startingVersion,
                    EventType = @event.GetType().Name,
                    Data = JsonSerializer.Serialize(@event, _serializerOptions),
                    Timestamp = DateTime.UtcNow
                };
                await _context.Events.AddAsync(storedEvent);
            }
        }
        catch (Exception ex)
        {

            throw;
        }
        
    }
}
