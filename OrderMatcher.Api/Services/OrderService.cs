using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Services;

public class OrderService : IOrderService
{
    private readonly List<Order> _orders = new();

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

        _orders.Add(order);
        return Task.FromResult(response);
    }

    public Task<IEnumerable<Order>> FindMatchesAsync(Order incomingOrder)
    {
        var eligibleStatuses = new[] { OrderStatus.Open, OrderStatus.PartiallyFilled };

        IEnumerable<Order> matches;

        if (incomingOrder.Side == OrderSide.Buy)
        {
            matches = _orders
                .Where(o => o.Symbol == incomingOrder.Symbol
                         && o.Side == OrderSide.Sell
                         && eligibleStatuses.Contains(o.Status)
                         && o.Price <= incomingOrder.Price)
                .OrderBy(o => o.Price)
                .ThenBy(o => o.CreatedAt);
        }
        else
        {
            matches = _orders
                .Where(o => o.Symbol == incomingOrder.Symbol
                         && o.Side == OrderSide.Buy
                         && eligibleStatuses.Contains(o.Status)
                         && o.Price >= incomingOrder.Price)
                .OrderByDescending(o => o.Price)
                .ThenBy(o => o.CreatedAt);
        }

        return Task.FromResult(matches);
    }

    public Task<IEnumerable<Trade>> MatchAndSettleAsync(Order incomingOrder)
    {
        throw new NotImplementedException();
    }
}
