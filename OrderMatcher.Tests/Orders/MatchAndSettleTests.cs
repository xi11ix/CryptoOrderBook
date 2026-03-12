using FluentAssertions;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Tests.Orders;

/// <summary>
/// Settlement rules:
///   - Matches are filled in price-time priority (same as FindMatchesAsync ordering)
///   - Each executed trade fills min(incoming remaining quantity, book order remaining quantity)
///   - Trade price is the resting (book) order's price
///   - Both the incoming order and each matched book order have their FilledQuantity and Status updated
///   - Status transitions: Open → PartiallyFilled → Filled
///   - If no matches exist the incoming order remains Open and no trades are produced
/// </summary>
public class MatchAndSettleTests
{
    private IOrderService NewService() => new OrderService();

    private Task<OrderResponse> SeedAsync(IOrderService svc, string symbol, OrderSide side, decimal price, decimal quantity = 1.0m)
        => svc.CreateOrderAsync(new CreateOrderRequest
        {
            Symbol = symbol,
            Side = side,
            Price = price,
            Quantity = quantity
        });

    // -------------------------------------------------------------------------
    // No matches
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_NoMatchingOrders_ReturnsNoTrades()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 51_000m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = await svc.MatchAndSettleAsync(incoming);

        trades.Should().BeEmpty();
    }

    [Fact]
    public async Task MatchAndSettle_NoMatchingOrders_IncomingOrderRemainsOpen()
    {
        var svc = NewService();

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.Open);
        incoming.FilledQuantity.Should().Be(0m);
    }

    // -------------------------------------------------------------------------
    // Single full match (incoming qty == book order qty)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_ReturnsOneTrade()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = await svc.MatchAndSettleAsync(incoming);

        trades.Should().HaveCount(1);
    }

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_TradeHasCorrectQuantityAndPrice()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 55_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades[0].Quantity.Should().Be(1.0m);
        trades[0].Price.Should().Be(50_000m); // resting order's price
    }

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_TradeLinksCorrectBuyAndSellOrderIds()
    {
        var svc = NewService();
        var sellResponse = await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades[0].BuyOrderId.Should().Be(incoming.Id);
        trades[0].SellOrderId.Should().Be(sellResponse.Id);
    }

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_IncomingOrderBecomeFilled()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.Filled);
        incoming.FilledQuantity.Should().Be(1.0m);
    }

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_BookOrderBecomeFilled()
    {
        var svc = NewService();
        var sellResponse = await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        var bookOrder = (await svc.FindMatchesAsync(
            new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = decimal.MaxValue, Quantity = 1m }))
            .FirstOrDefault(o => o.Id == sellResponse.Id);

        // Filled orders should no longer appear as match candidates
        bookOrder.Should().BeNull();
    }

    [Fact]
    public async Task MatchAndSettle_ExactSizeMatch_TradeSymbolIsCorrect()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades[0].Symbol.Should().Be("BTC/USD");
        trades[0].Id.Should().NotBeEmpty();
        trades[0].ExecutedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(10));
    }

    // -------------------------------------------------------------------------
    // Incoming order larger than book order (partial fill of incoming)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_IncomingLargerThanBookOrder_IncomingBecomesPartiallyFilled()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.5m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.PartiallyFilled);
        incoming.FilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public async Task MatchAndSettle_IncomingLargerThanBookOrder_TradeQuantityEqualsBookOrderQuantity()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.3m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades.Should().HaveCount(1);
        trades[0].Quantity.Should().Be(0.3m);
    }

    // -------------------------------------------------------------------------
    // Incoming order smaller than book order (partial fill of book order)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_IncomingSmallerThanBookOrder_IncomingBecomeFilled()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 2.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 0.5m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.Filled);
        incoming.FilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public async Task MatchAndSettle_IncomingSmallerThanBookOrder_BookOrderBecomesPartiallyFilled()
    {
        var svc = NewService();
        var sellResponse = await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 2.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 0.5m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        // The book order is PartiallyFilled so it should still appear as a match candidate
        var remaining = (await svc.FindMatchesAsync(
            new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = decimal.MaxValue, Quantity = 1m }))
            .FirstOrDefault(o => o.Id == sellResponse.Id);

        remaining.Should().NotBeNull();
        remaining!.Status.Should().Be(OrderStatus.PartiallyFilled);
        remaining.FilledQuantity.Should().Be(0.5m);
    }

    // -------------------------------------------------------------------------
    // Multiple book orders consumed in priority order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_MultipleMatches_TradesCreatedForEachBookOrder()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, quantity: 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.5m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades.Should().HaveCount(2);
    }

    [Fact]
    public async Task MatchAndSettle_MultipleMatches_IncomingOrderBecomeFilled()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, quantity: 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.5m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.Filled);
        incoming.FilledQuantity.Should().Be(1.0m);
    }

    [Fact]
    public async Task MatchAndSettle_MultipleMatches_TradesFilledInPriceTimePriority()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.5m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, quantity: 0.5m); // better price, but seeded later

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        // Lowest ask should be matched first
        trades[0].Price.Should().Be(49_000m);
        trades[1].Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task MatchAndSettle_IncomingPartiallyFilledAcrossMultipleBookOrders_RemainingQuantityIsCorrect()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 49_000m, quantity: 0.3m);
        await SeedAsync(svc, "BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.3m);
        // Total available = 0.6, incoming = 1.0 → PartiallyFilled with 0.6 filled

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.PartiallyFilled);
        incoming.FilledQuantity.Should().Be(0.6m);
    }

    // -------------------------------------------------------------------------
    // Sell-side incoming order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MatchAndSettle_IncomingSellOrder_MatchesAgainstBuyOrders()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 52_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        var trades = (await svc.MatchAndSettleAsync(incoming)).ToList();

        trades.Should().HaveCount(1);
        trades[0].Price.Should().Be(52_000m); // resting buy order's price
    }

    [Fact]
    public async Task MatchAndSettle_IncomingSellOrder_IncomingBecomeFilled()
    {
        var svc = NewService();
        await SeedAsync(svc, "BTC/USD", OrderSide.Buy, 52_000m, quantity: 1.0m);

        var incoming = new Order { Id = Guid.NewGuid(), Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m, Status = OrderStatus.Open };
        await svc.MatchAndSettleAsync(incoming);

        incoming.Status.Should().Be(OrderStatus.Filled);
        incoming.FilledQuantity.Should().Be(1.0m);
    }
}
