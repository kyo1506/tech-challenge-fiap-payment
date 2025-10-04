using Shared.DTOs.Commands;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services;

public interface IPurchaseApplicationService
{
    Task<PurchaseResponse> CreatePurchaseAsync(CreatePurchaseCommand command);
    Task<RefundResponse> CreateRefundAsync(CreateRefundCommand command);
}
