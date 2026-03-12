using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal FilledQuantity { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
