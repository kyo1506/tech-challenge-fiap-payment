using Domain.Aggregates;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events;

public record PurchaseCreated(Guid PurchaseId, Guid UserId, List<PurchaseItem> Items, decimal TotalPrice);
public record PurchaseCompleted(Guid PurchaseId);
public record PurchaseFailed(Guid PurchaseId, string Reason);
public record PurchaseRefunded(Guid PurchaseId);
