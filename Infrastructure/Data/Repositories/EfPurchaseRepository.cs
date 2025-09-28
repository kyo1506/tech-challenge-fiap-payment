using Domain.Interfaces.Repositories;
using Infrastructure.Data.EventSourcing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.Repositories;

public class EfPurchaseRepository : GenericEventSourcedRepository<Purchase>, IPurchaseRepository
{
    public EfPurchaseRepository(EventStoreDbContext context) : base(context) { }

    public async Task<Purchase?> GetByIdAsync(Guid purchaseId)
    {
        return await LoadAsync(purchaseId);
    }

    public async Task StoreAsync(Purchase purchase)
    {
        await base.StoreAsync(purchase);
    }
}