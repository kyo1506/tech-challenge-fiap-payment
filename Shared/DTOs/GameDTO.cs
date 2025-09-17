using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs;

public class ShoppingDTO
{
    public List<GameDTO> Items { get; set; } = new();
}

public class GameDTO
{
    public Guid GameId { get; set; }
    public string GameName { get; set; }
    public decimal FinalPrice { get; set; }
}