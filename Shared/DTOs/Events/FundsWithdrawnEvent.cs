using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Events;
public class FundsWithdrawnEvent
{
    public Guid UserId { get; }
    public decimal Amount { get; }
    public DateTime Timestamp { get; }

    public FundsWithdrawnEvent(Guid userId, decimal amount)
    {
        UserId = userId;
        Amount = amount;
        Timestamp = DateTime.UtcNow;
    }
}