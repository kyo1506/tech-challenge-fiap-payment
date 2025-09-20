using Domain.Aggregates;
using Domain.Interfaces.Repositories;
using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Repositories;
public class MartenPurchaseRepository(IDocumentSession _session) : IPurchaseRepository
{
    public async Task<Purchase?> GetByIdAsync(Guid purchaseId)
    {
        var events = await _session.Events.FetchStreamAsync(purchaseId);

        if (events == null || !events.Any())
        {
            return null;
        }
        var eventData = events.Select(e => e.Data).ToList();

        var purchase = new Purchase(eventData);

        return purchase;
    }

    public Task StoreAsync(Purchase purchase)
    {
        var events = purchase.GetUncommittedEvents().ToArray();
        if (!events.Any())
            return Task.CompletedTask;

        var isNew = purchase.Version == events.Length;

        if (isNew)
        {
            _session.Events.StartStream<Purchase>(purchase.Id, events);
        }
        else
        {
            var expectedVersion = purchase.Version - events.Length;
            _session.Events.Append(purchase.Id, expectedVersion, events);
        }

        return Task.CompletedTask;
    }
}
