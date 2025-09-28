namespace Domain.Aggregates;
public class PurchaseItem
{
    public Guid GameId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal FinalPrice => CalculateFinalPrice();

    private decimal CalculateFinalPrice()
    {
        if (DiscountPercentage.HasValue && DiscountPercentage > 0)
        {
            return OriginalPrice * (1 - DiscountPercentage.Value / 100);
        }
        return OriginalPrice;
    }
}