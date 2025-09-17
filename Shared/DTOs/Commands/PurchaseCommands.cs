using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;

public record CreatePurchaseCommand(
    [Required] Guid UserId,
    [Required][MinLength(1)] List<Guid> GameIds);

public record CreateRefundCommand(
    [Required] Guid PurchaseId,
    string? Reason);