using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events;

public class WalletEvents { 
    public record WalletCreated(Guid UserId);
    public record FundsDeposited(Guid WalletId, decimal Amount);
    public record FundsWithdrawn(Guid WalletId, decimal Amount, string Reason = "Withdrawal");
    public record PurchasePaymentMade(Guid WalletId, Guid PurchaseId, decimal Amount);
    public record PurchaseRefunded(Guid WalletId, Guid PurchaseId, decimal Amount);
}
