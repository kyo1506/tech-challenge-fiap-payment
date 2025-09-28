using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class RefundResponse
{
    public Guid PurchaseId { get; set; }

    public decimal RefundAmount { get; set; }

    public decimal NewBalance { get; set; }
}