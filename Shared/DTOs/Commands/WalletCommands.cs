using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;
public record CreateDepositCommand(Guid UserId, decimal Amount);
public record CreateWithdrawalCommand(Guid UserId, decimal Amount);

