using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Responses;

public class BalanceResponse
{
    public Guid UserId { get; set; }
    public string Balance { get; set; }

}
