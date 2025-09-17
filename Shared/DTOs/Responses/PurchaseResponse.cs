using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class PurchaseResponse
{
    public Guid PurchaseId { get; set; }

    public string Status { get; set; }

    public decimal TotalPrice { get; set; }

    public decimal NewBalance { get; set; }

    public List<PurchaseItemResponse> Items { get; set; }
}