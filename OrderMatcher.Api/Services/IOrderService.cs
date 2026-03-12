using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<IEnumerable<Order>> FindMatchesAsync(Order incomingOrder);
}
