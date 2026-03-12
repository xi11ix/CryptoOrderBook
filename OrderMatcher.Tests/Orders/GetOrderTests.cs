using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Data;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Tests.Orders;

public class GetOrderTests
{
    private static OrderService CreateService()
    {
        var options = new DbContextOptionsBuilder<OrderMatcherDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrderService(new OrderMatcherDbContext(options));
    }

    private static Task<OrderResponse> SeedAsync(OrderService svc, string symbol, OrderSide side, decimal price, decimal quantity = 1.0m)
        => svc.CreateOrderAsync(new CreateOrderRequest
        {
            Symbol = symbol,
            Side = side,
            Price = price,
            Quantity = quantity
        });

    [Fact]
    public async Task GetOrder_WithExistingId_ReturnsOrder()
    {
        var svc = CreateService();
        var created = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var result = await svc.GetOrderAsync(created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetOrder_WithExistingId_ReturnsCorrectFields()
    {
        var svc = CreateService();
        var created = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.5m);

        var result = await svc.GetOrderAsync(created.Id);

        result!.Symbol.Should().Be("BTC/USD");
        result.Side.Should().Be(OrderSide.Buy);
        result.Price.Should().Be(50_000m);
        result.Quantity.Should().Be(1.5m);
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_ReturnsNull()
    {
        var svc = CreateService();

        var result = await svc.GetOrderAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrder_ReflectsCurrentStatus_WhenOrderIsOpen()
    {
        var svc = CreateService();
        var created = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var result = await svc.GetOrderAsync(created.Id);

        result!.Status.Should().Be(OrderStatus.Open);
        result.FilledQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task GetOrder_ReflectsCurrentStatus_WhenOrderIsFilled()
    {
        var svc = CreateService();
        // Sell resting, then matching buy triggers settlement
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 1.0m);
        var buy = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var result = await svc.GetOrderAsync(buy.Id);

        result!.Status.Should().Be(OrderStatus.Filled);
        result.FilledQuantity.Should().Be(1.0m);
    }

    [Fact]
    public async Task GetOrder_ReflectsCurrentStatus_WhenOrderIsPartiallyFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 0.4m);
        var buy = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var result = await svc.GetOrderAsync(buy.Id);

        result!.Status.Should().Be(OrderStatus.PartiallyFilled);
        result.FilledQuantity.Should().Be(0.4m);
    }

    [Fact]
    public async Task GetOrder_DoesNotReturnAnotherOrdersData()
    {
        var svc = CreateService();
        var first = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);
        await SeedAsync(svc, "ETH/USD", OrderSide.Sell, 3_000m, 2.0m);

        var result = await svc.GetOrderAsync(first.Id);

        result!.Symbol.Should().Be("BTC/USD");
        result.Side.Should().Be(OrderSide.Buy);
    }
}
