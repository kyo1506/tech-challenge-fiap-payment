using Shared.DTOs.Commands;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services;

public interface IWalletApplicationService
{
    Task<TransactionResponse> CreateDepositAsync(CreateDepositCommand command);
    Task<TransactionResponse> CreateWithdrawalAsync(CreateWithdrawalCommand command);
    Task<BalanceResponse> GetBalanceAsync(Guid userId);
    Task<TransactionHistoryResponse> GetTransactionHistoryAsync(Guid userId);
}