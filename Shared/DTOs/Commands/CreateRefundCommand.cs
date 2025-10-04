using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;

public record CreateRefundCommand(
    [property: JsonPropertyName("purchaseId")] [Required] Guid PurchaseId,
    [property: JsonPropertyName("reason")] string? Reason);