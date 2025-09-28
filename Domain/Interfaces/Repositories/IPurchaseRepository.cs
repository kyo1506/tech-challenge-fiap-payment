using Domain.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid purchaseId);
    Task StoreAsync(Purchase purchase);
}