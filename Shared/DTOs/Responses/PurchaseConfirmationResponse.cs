using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class PurchaseConfirmationResponse
{
    public Guid UserId { get; set; }
    public Guid PaymentTransactionId { get; set; } 
    public List<PurchaseConfirmationItem> Games { get; set; } = new();
}
