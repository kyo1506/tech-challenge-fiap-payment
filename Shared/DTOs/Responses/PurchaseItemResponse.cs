using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class PurchaseItemResponse
{
    public Guid HistoryPaymentId { get; set; }
    public Guid GameId { get; set; }
    public string GameName { get; set; }
    public decimal PricePaid { get; set; }
}