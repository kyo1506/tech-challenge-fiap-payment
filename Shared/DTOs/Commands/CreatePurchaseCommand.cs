using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.DTOs.Commands;

public class CreatePurchaseCommand
{
    [JsonPropertyName("userId")]
    [Required]
    public Guid UserId { get; set; }
    [JsonPropertyName("games")]
    [Required]
    [MinLength(1)]
    public List<GamePurchaseItemCommand> Games { get; set; } = new();
}

public class GamePurchaseItemCommand
{
    [Required]
    [JsonPropertyName("historyPaymentId")]
    public Guid HistoryPaymentId { get; set; }

    [Required]
    [JsonPropertyName("gameId")]
    public Guid GameId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    [JsonPropertyName("price")]
    public decimal Price { get; set; }
    [JsonPropertyName("promotionId")]
    public Guid? PromotionId { get; set; }
    [JsonPropertyName("discount")]
    [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100.")]
    public decimal? Discount { get; set; }
}

