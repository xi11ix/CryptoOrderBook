using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Services;

public class OrderService : IOrderService
{
    public Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Symbol = request.Symbol,
            Side = request.Side,
            Price = request.Price,
            Quantity = request.Quantity,
            FilledQuantity = 0m,
            Status = OrderStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        var response = new OrderResponse
        {
            Id = order.Id,
            Symbol = order.Symbol,
            Side = order.Side,
            Price = order.Price,
            Quantity = order.Quantity,
            FilledQuantity = order.FilledQuantity,
            Status = order.Status,
            CreatedAt = order.CreatedAt
        };

        return Task.FromResult(response);
    }
}
