using Microsoft.AspNetCore.Mvc;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = await _orderService.CreateOrderAsync(request);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("order/{id:guid}")]
    public IActionResult GetOrder(Guid id)
    {
        throw new NotImplementedException();
    }

    [HttpGet("orderbook/{symbol}")]
    public async Task<IActionResult> GetOrderBook(string symbol)
    {
        var book = await _orderService.GetOrderBookAsync(symbol);
        return Ok(book);
    }
}
