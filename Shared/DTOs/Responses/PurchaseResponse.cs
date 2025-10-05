using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class PurchaseResponse
{
    public string CommandType { get; set; } = "create-purchase";
    public Guid UserId { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public List<PurchaseItemResponse> Games{ get; set; } = new();
}