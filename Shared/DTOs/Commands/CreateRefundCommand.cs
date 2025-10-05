using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;

public record CreateRefundCommand(
    [property: JsonPropertyName("UserId")] [Required] Guid UserId,
    [property: JsonPropertyName("PaymentTransactionId")] Guid PaymentTransactionId);