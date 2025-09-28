using Application.Interfaces.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.EventSourcing;

public class EventStoreUnitOfWork : IEventStoreUnitOfWork
{
    private readonly EventStoreDbContext _context;
    public EventStoreUnitOfWork(EventStoreDbContext context) => _context = context;
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}