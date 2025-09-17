using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Events;
public class FundsDepositedEvent
{
    public Guid UserId { get; }
    public decimal Amount { get; }
    public DateTime Timestamp { get; }

    public FundsDepositedEvent(Guid userId, decimal amount)
    {
        UserId = userId;
        Amount = amount;
        Timestamp = DateTime.UtcNow;
    }
}