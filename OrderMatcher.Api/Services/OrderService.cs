using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Services;

public class OrderService : IOrderService
{
    public Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        throw new NotImplementedException();
    }
}
