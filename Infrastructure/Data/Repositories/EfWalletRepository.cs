using Domain.Aggregates;
using Domain.Events;
using Domain.Interfaces.Repositories;
using Domain.QueryModels;
using Infrastructure.Data.EventSourcing;
using JasperFx.Events;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Domain.Events.WalletEvents;

namespace Infrastructure.Data.Repositories;

public class EfWalletRepository : GenericEventSourcedRepository<UserWallet>, IWalletRepository
{
    public EfWalletRepository(EventStoreDbContext context) : base(context) { }
    private readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<UserWallet?> GetByUserIdAsync(Guid userId)
    {
        return await LoadAsync(userId);
    }

    public async Task StoreAsync(UserWallet wallet)
    {
        await base.StoreAsync(wallet);
    }

    public async Task<IReadOnlyList<HistoricEvent>> GetHistoryAsync(Guid userId)
    {
        var storedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => e.StreamId == userId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        if (!storedEvents.Any())
        {
            return new List<HistoricEvent>(); 
        }

        var history = storedEvents.Select(se =>
        {
            Type eventType = se.EventType switch
            {
                nameof(WalletCreated) => typeof(WalletCreated),
                nameof(FundsDeposited) => typeof(FundsDeposited),
                nameof(FundsWithdrawn) => typeof(FundsWithdrawn),
                nameof(PurchasePaymentMade) => typeof(PurchasePaymentMade),
                nameof(PurchaseRefunded) => typeof(PurchaseRefunded),
                _ => typeof(object) 
            };

            var eventData = JsonSerializer.Deserialize(se.Data, eventType, _serializerOptions);

            return new HistoricEvent
            {
                Version = se.Version,
                EventData = eventData,
                Timestamp = se.Timestamp
            };
        }).ToList();

        return history;
    }
}
