namespace OrderMatcher.Api.Models;

public class Trade
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public Guid BuyOrderId { get; set; }
    public Guid SellOrderId { get; set; }
    /// <summary>Price of the resting (book) order at the time of execution.</summary>
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public DateTime ExecutedAt { get; set; }
}
