namespace OrderMatcher.Api.DTOs;

/// <summary>
/// A single price level in the order book, aggregating all resting orders at that price.
/// </summary>
public class OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal TotalQuantity { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// Snapshot of the order book for a single symbol.
/// Bids (buy side) ordered highest price first.
/// Asks (sell side) ordered lowest price first.
/// Only Open and PartiallyFilled orders are included.
/// </summary>
public class OrderBookResponse
{
    public string Symbol { get; set; } = string.Empty;
    public IReadOnlyList<OrderBookLevel> Bids { get; set; } = [];
    public IReadOnlyList<OrderBookLevel> Asks { get; set; } = [];
}
