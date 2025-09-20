using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;

public class CreatePurchaseCommand
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MinLength(1)]
    public List<GamePurchaseItemCommand> Games { get; set; } = new();
}

public class GamePurchaseItemCommand
{
    [Required]
    public Guid HistoryPaymentId { get; set; }

    [Required]
    public Guid GameId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public Guid? PromotionId { get; set; }

    [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100.")]
    public decimal? Discount { get; set; }
}

