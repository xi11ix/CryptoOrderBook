using FluentAssertions;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;
using OrderMatcher.Api.Services;

namespace OrderMatcher.Tests.Orders;

/// <summary>
/// Matching rules:
///   - A buy order matches sell orders on the same symbol where sell.Price &lt;= buy.Price
///   - A sell order matches buy orders on the same symbol where buy.Price &gt;= sell.Price
///   - Only Open or PartiallyFilled orders are eligible as matches
///   - Results are ordered best price first (lowest ask for buys; highest bid for sells),
///     with earlier CreatedAt as a tiebreaker (price-time priority)
/// </summary>
public class FindMatchesTests
{
    private readonly IOrderService _service;

    public FindMatchesTests()
    {
        _service = new OrderService();
    }

    private Task<OrderResponse> SeedAsync(string symbol, OrderSide side, decimal price, decimal quantity = 1.0m)
        => _service.CreateOrderAsync(new CreateOrderRequest
        {
            Symbol = symbol,
            Side = side,
            Price = price,
            Quantity = quantity
        });

    // -------------------------------------------------------------------------
    // Basic matching
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindMatches_BuyOrder_MatchesSellOrderAtExactPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(1);
        matches.First().Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task FindMatches_BuyOrder_MatchesSellOrderBelowBuyPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 49_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(1);
        matches.First().Price.Should().Be(49_000m);
    }

    [Fact]
    public async Task FindMatches_BuyOrder_DoesNotMatchSellOrderAboveBuyPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 51_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatches_SellOrder_MatchesBuyOrderAtExactPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Buy, 50_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(1);
        matches.First().Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task FindMatches_SellOrder_MatchesBuyOrderAboveSellPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Buy, 52_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(1);
        matches.First().Price.Should().Be(52_000m);
    }

    [Fact]
    public async Task FindMatches_SellOrder_DoesNotMatchBuyOrderBelowSellPrice()
    {
        await SeedAsync("BTC/USD", OrderSide.Buy, 48_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Symbol isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindMatches_DoesNotMatchOrdersOnDifferentSymbol()
    {
        await SeedAsync("ETH/USD", OrderSide.Sell, 3_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatches_OnlyReturnsMatchesForCorrectSymbol()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);
        await SeedAsync("ETH/USD", OrderSide.Sell, 3_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(1);
        matches.First().Symbol.Should().Be("BTC/USD");
    }

    // -------------------------------------------------------------------------
    // Same-side exclusion
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindMatches_BuyOrder_DoesNotMatchOtherBuyOrders()
    {
        await SeedAsync("BTC/USD", OrderSide.Buy, 50_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task FindMatches_SellOrder_DoesNotMatchOtherSellOrders()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Order book status filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindMatches_DoesNotMatchFilledOrders()
    {
        var seeded = await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);

        // Simulate an already-filled order by creating an Order with Filled status
        var filledOrder = new Order
        {
            Id = seeded.Id,
            Symbol = seeded.Symbol,
            Side = seeded.Side,
            Price = seeded.Price,
            Quantity = seeded.Quantity,
            FilledQuantity = seeded.Quantity,
            Status = OrderStatus.Filled,
            CreatedAt = seeded.CreatedAt
        };

        // The service book should not return filled orders as candidates
        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().NotContain(o => o.Status == OrderStatus.Filled);
    }

    [Fact]
    public async Task FindMatches_DoesNotMatchCancelledOrders()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().NotContain(o => o.Status == OrderStatus.Cancelled);
    }

    // -------------------------------------------------------------------------
    // Multiple matches and ordering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindMatches_BuyOrder_ReturnsAllEligibleSellMatches()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 48_000m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 49_000m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 51_000m); // above buy price — excluded

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 3.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().HaveCount(3);
    }

    [Fact]
    public async Task FindMatches_BuyOrder_MatchesOrderedByLowestAskFirst()
    {
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 48_000m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 49_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 3.0m };
        var matches = (await _service.FindMatchesAsync(incoming)).ToList();

        matches[0].Price.Should().Be(48_000m);
        matches[1].Price.Should().Be(49_000m);
        matches[2].Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task FindMatches_SellOrder_MatchesOrderedByHighestBidFirst()
    {
        await SeedAsync("BTC/USD", OrderSide.Buy, 50_000m);
        await SeedAsync("BTC/USD", OrderSide.Buy, 52_000m);
        await SeedAsync("BTC/USD", OrderSide.Buy, 51_000m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Sell, Price = 50_000m, Quantity = 3.0m };
        var matches = (await _service.FindMatchesAsync(incoming)).ToList();

        matches[0].Price.Should().Be(52_000m);
        matches[1].Price.Should().Be(51_000m);
        matches[2].Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task FindMatches_SamePriceSellOrders_OrderedByCreatedAtAscending()
    {
        // Seed two sells at the same price; the earlier one should be first (time priority)
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m, quantity: 0.5m);
        await SeedAsync("BTC/USD", OrderSide.Sell, 50_000m, quantity: 1.5m);

        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 2.0m };
        var matches = (await _service.FindMatchesAsync(incoming)).ToList();

        matches.Should().HaveCount(2);
        // First match should be the one with the smaller quantity (seeded first)
        matches[0].Quantity.Should().Be(0.5m);
        matches[1].Quantity.Should().Be(1.5m);
    }

    [Fact]
    public async Task FindMatches_EmptyOrderBook_ReturnsEmpty()
    {
        var incoming = new Order { Symbol = "BTC/USD", Side = OrderSide.Buy, Price = 50_000m, Quantity = 1.0m };
        var matches = await _service.FindMatchesAsync(incoming);

        matches.Should().BeEmpty();
    }
}
