using OrderMatcher.Api.DTOs;

namespace OrderMatcher.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
}
