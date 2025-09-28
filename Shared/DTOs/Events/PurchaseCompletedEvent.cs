using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Events;

public class PurchaseCompletedEvent
{
    public Guid PurchaseId { get; }
    public Guid UserId { get; }
    public List<Guid> GameIds { get; }

    public PurchaseCompletedEvent(Guid purchaseId, Guid userId, List<Guid> gameIds)
    {
        PurchaseId = purchaseId;
        UserId = userId;
        GameIds = gameIds;
    }
}