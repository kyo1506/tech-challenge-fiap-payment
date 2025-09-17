namespace Domain.Aggregates;
public class PurchaseItem
{
    public Guid GameId { get; set; }
    public decimal Price { get; set; }
    public string GameName { get; set; }
}