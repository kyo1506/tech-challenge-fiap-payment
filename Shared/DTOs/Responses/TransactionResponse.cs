using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class TransactionResponse
{
    public Guid TransactionId { get; set; }

    public string TransactionType { get; set; }

    public decimal Amount { get; set; }

    public decimal NewBalance { get; set; }
}