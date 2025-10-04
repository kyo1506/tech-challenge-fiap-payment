using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class PurchaseItemResponse
{
    public Guid GameId { get; set; }
    public decimal Price { get; set; }
    public Guid? PromotionId { get; set; }
    public decimal? Discount { get; set; }
    public Guid HistoryPaymentId { get; set; }
}