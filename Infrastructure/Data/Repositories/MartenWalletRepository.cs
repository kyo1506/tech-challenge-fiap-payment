using Domain.Aggregates;
using Domain.Interfaces.Repositories;
using JasperFx.Events;
using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Repositories;

public class MartenWalletRepository(IDocumentSession _session) : IWalletRepository
{
    public async Task<UserWallet?> GetByUserIdAsync(Guid userId)
    {
        var wallet = await _session.Events.AggregateStreamAsync<UserWallet>(userId);
        if (wallet != null)
        {
            var state = await _session.Events.FetchStreamStateAsync(userId);
            wallet.LoadOriginalVersion(state?.Version ?? 0);
        }
        return wallet;
    }
    public async Task StoreAsync(UserWallet wallet)
    {
        try
        {
            var events = wallet.GetUncommittedEvents().ToArray();
            if (!events.Any())
                return;
            var state = await _session.Events.FetchStreamStateAsync(wallet.Id);
            var expectedVersion = (state?.Version ?? 0)+ events.Length;
            if (wallet.GetOriginalVersion() == 0)
            {
                _session.Events.StartStream<UserWallet>(wallet.Id, events);
            }
            else
            {
                _session.Events.Append(wallet.Id, expectedVersion, events);
            }

            await _session.SaveChangesAsync();

            wallet.MarkEventsAsCommitted();
        }
        catch (Exception e)
        {

            throw;
        }
        
    }
    public async Task<IReadOnlyList<IEvent>> GetHistoryAsync(Guid userId)
    {
        var events = await _session.Events.FetchStreamAsync(userId);
        return events;
    }
}
