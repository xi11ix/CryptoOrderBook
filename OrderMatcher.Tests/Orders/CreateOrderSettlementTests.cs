using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Data;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Tests.Orders;

/// <summary>
/// Verifies that CreateOrderAsync automatically settles the incoming order
/// against any resting orders in the book immediately after insertion.
/// </summary>
public class CreateOrderSettlementTests
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

    // -------------------------------------------------------------------------
    // No match — order stays Open
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_NoMatchingCounterpart_ResponseStatusIsOpen()
    {
        var svc = CreateService();

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        response.Status.Should().Be(OrderStatus.Open);
        response.FilledQuantity.Should().Be(0m);
    }

    // -------------------------------------------------------------------------
    // Exact match — both sides fully filled
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_ExactMatchExists_NewOrderReturnedAsFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 1.0m);

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        response.Status.Should().Be(OrderStatus.Filled);
        response.FilledQuantity.Should().Be(1.0m);
    }

    [Fact]
    public async Task CreateOrder_ExactMatchExists_RestingOrderBecomeFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 1.0m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        // Filled orders must no longer appear in the book
        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Asks.Should().BeEmpty();
        book.Bids.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Partial fill — incoming larger than resting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_IncomingLargerThanResting_ResponseIsPartiallyFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 0.5m);

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        response.Status.Should().Be(OrderStatus.PartiallyFilled);
        response.FilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public async Task CreateOrder_IncomingLargerThanResting_RemainingQuantityAppearsInBook()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        // Incoming buy is PartiallyFilled — 0.5 remaining should sit in bids
        book.Bids.Should().HaveCount(1);
        book.Bids[0].TotalQuantity.Should().Be(0.5m);
        book.Asks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Partial fill — incoming smaller than resting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_IncomingSmallerThanResting_ResponseIsFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 2.0m);

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 0.5m);

        response.Status.Should().Be(OrderStatus.Filled);
        response.FilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public async Task CreateOrder_IncomingSmallerThanResting_RestingOrderRemainsInBook()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 2.0m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 0.5m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Asks.Should().HaveCount(1);
        book.Asks[0].TotalQuantity.Should().Be(1.5m);
        book.Bids.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Multiple resting orders consumed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_MultipleRestingOrders_AllConsumedAndNewOrderFilled()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 0.5m);

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        response.Status.Should().Be(OrderStatus.Filled);
        response.FilledQuantity.Should().Be(1.0m);
    }

    [Fact]
    public async Task CreateOrder_MultipleRestingOrders_BookIsEmptyAfterFullFill()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().BeEmpty();
        book.Asks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Symbol isolation — settlement does not cross symbols
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrder_RestingOrderOnDifferentSymbol_NewOrderRemainsOpen()
    {
        var svc = CreateService();
        await SeedAsync(svc, "ETH/USD", OrderSide.Sell, 50_000m, 1.0m);

        var response = await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        response.Status.Should().Be(OrderStatus.Open);
        response.FilledQuantity.Should().Be(0m);
    }
}
