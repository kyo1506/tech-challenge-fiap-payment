using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Event;

public interface IEventStoreUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}