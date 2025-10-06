using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class TransactionHistoryResponse
{
    public Guid UserId { get; set; }
    public string CurrentBalance { get; set; }
    public List<TransactionHistoryItem> History { get; set; } = new();
}

public class TransactionHistoryItem
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}
