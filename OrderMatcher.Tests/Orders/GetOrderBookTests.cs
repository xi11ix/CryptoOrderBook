using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Data;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Tests.Orders;

/// <summary>
/// Order book rules:
///   - Only Open and PartiallyFilled orders are included
///   - Bids (buy side) are aggregated by price, ordered highest first
///   - Asks (sell side) are aggregated by price, ordered lowest first
///   - Each level reports the total remaining (unfilled) quantity and order count at that price
///   - Only orders for the requested symbol are included
/// </summary>
public class GetOrderBookTests
{
    private static OrderService CreateService()
    {
        var options = new DbContextOptionsBuilder<OrderMatcherDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrderService(new OrderMatcherDbContext(options));
    }

    private Task<OrderResponse> SeedAsync(OrderService svc, string symbol, OrderSide side, decimal price, decimal quantity = 1.0m)
        => svc.CreateOrderAsync(new CreateOrderRequest
        {
            Symbol = symbol,
            Side = side,
            Price = price,
            Quantity = quantity
        });

    // -------------------------------------------------------------------------
    // Empty book
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_EmptyBook_ReturnsBothSidesEmpty()
    {
        var svc = CreateService();

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Symbol.Should().Be("BTC/USD");
        book.Bids.Should().BeEmpty();
        book.Asks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Bids (buy side)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_SingleBuyOrder_AppearsInBids()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.0m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().HaveCount(1);
        book.Bids[0].Price.Should().Be(50_000m);
        book.Bids[0].TotalQuantity.Should().Be(1.0m);
        book.Bids[0].OrderCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrderBook_MultipleBuyOrdersAtDifferentPrices_OrderedHighestFirst()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 48_000m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 49_000m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().HaveCount(3);
        book.Bids[0].Price.Should().Be(50_000m);
        book.Bids[1].Price.Should().Be(49_000m);
        book.Bids[2].Price.Should().Be(48_000m);
    }

    [Fact]
    public async Task GetOrderBook_MultipleOrdersAtSameBidPrice_AggregatedIntoOneLevel()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 1.5m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().HaveCount(1);
        book.Bids[0].Price.Should().Be(50_000m);
        book.Bids[0].TotalQuantity.Should().Be(2.0m);
        book.Bids[0].OrderCount.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Asks (sell side)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_SingleSellOrder_AppearsInAsks()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m, 2.0m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Asks.Should().HaveCount(1);
        book.Asks[0].Price.Should().Be(51_000m);
        book.Asks[0].TotalQuantity.Should().Be(2.0m);
        book.Asks[0].OrderCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrderBook_MultipleSellOrdersAtDifferentPrices_OrderedLowestFirst()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 53_000m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 52_000m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Asks.Should().HaveCount(3);
        book.Asks[0].Price.Should().Be(51_000m);
        book.Asks[1].Price.Should().Be(52_000m);
        book.Asks[2].Price.Should().Be(53_000m);
    }

    [Fact]
    public async Task GetOrderBook_MultipleOrdersAtSameAskPrice_AggregatedIntoOneLevel()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m, 1.0m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m, 2.0m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Asks.Should().HaveCount(1);
        book.Asks[0].Price.Should().Be(51_000m);
        book.Asks[0].TotalQuantity.Should().Be(3.0m);
        book.Asks[0].OrderCount.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Remaining quantity (partially filled orders)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_PartiallyFilledOrder_LevelReflectsRemainingQuantity()
    {
        var svc = CreateService();
        // Seed a sell at 51k qty 2, then a buy at 51k qty 1 — sell becomes partially filled (1 remaining)
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m, 2.0m);
        var buy = new Order
        {
            Id = Guid.NewGuid(),
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 51_000m,
            Quantity = 1.0m,
            Status = OrderStatus.Open
        };
        await svc.MatchAndSettleAsync(buy);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        // The sell is now PartiallyFilled with 1.0 remaining
        book.Asks.Should().HaveCount(1);
        book.Asks[0].TotalQuantity.Should().Be(1.0m);
    }

    // -------------------------------------------------------------------------
    // Status filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_FilledOrders_ExcludedFromBook()
    {
        var svc = CreateService();
        // Seed a sell at 50k qty 1, then a matching buy — both become Filled
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, 1.0m);
        var buy = new Order
        {
            Id = Guid.NewGuid(),
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 50_000m,
            Quantity = 1.0m,
            Status = OrderStatus.Open
        };
        await svc.MatchAndSettleAsync(buy);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().BeEmpty();
        book.Asks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Symbol isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_OnlyReturnsOrdersForRequestedSymbol()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m);
        await SeedAsync(svc, "ETH/USD", OrderSide.Buy, 3_000m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().HaveCount(1);
        book.Bids[0].Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task GetOrderBook_UnknownSymbol_ReturnsBothSidesEmpty()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m);

        var book = await svc.GetOrderBookAsync("SOL/USD");

        book.Symbol.Should().Be("SOL/USD");
        book.Bids.Should().BeEmpty();
        book.Asks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Both sides present simultaneously
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrderBook_BothSidesPresent_BidsAndAsksPopulatedCorrectly()
    {
        var svc = CreateService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 49_000m, 1.0m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 50_000m, 2.0m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m, 1.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 52_000m, 0.5m);

        var book = await svc.GetOrderBookAsync("BTC/USD");

        book.Bids.Should().HaveCount(2);
        book.Asks.Should().HaveCount(2);
        book.Bids[0].Price.Should().Be(50_000m);
        book.Asks[0].Price.Should().Be(51_000m);
    }
}
