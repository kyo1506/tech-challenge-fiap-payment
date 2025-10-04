using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;
public record CreateDepositCommand(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("amount")] decimal Amount);
public record CreateWithdrawalCommand(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("amount")] decimal Amount);
