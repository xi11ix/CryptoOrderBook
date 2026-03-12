using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Tests.Orders;

public class CreateOrderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CreateOrderTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_WithValidBuyOrder_Returns201Created()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 50_000m,
            Quantity = 0.5m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrder_WithValidSellOrder_Returns201Created()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "ETH/USD",
            Side = OrderSide.Sell,
            Price = 3_000m,
            Quantity = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsCreatedOrderBody()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "ETH/USD",
            Side = OrderSide.Sell,
            Price = 3_000m,
            Quantity = 2.0m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        order.Should().NotBeNull();
        order!.Id.Should().NotBeEmpty();
        order.Symbol.Should().Be("ETH/USD");
        order.Side.Should().Be(OrderSide.Sell);
        order.Price.Should().Be(3_000m);
        order.Quantity.Should().Be(2.0m);
        order.FilledQuantity.Should().Be(0m);
        order.Status.Should().Be(OrderStatus.Open);
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_LocationHeaderPointsToNewOrder()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 50_000m,
            Quantity = 1.0m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(order!.Id.ToString());
    }

    [Fact]
    public async Task CreateOrder_WithMissingSymbol_Returns400BadRequest()
    {
        var request = new { Side = OrderSide.Buy, Price = 50_000m, Quantity = 0.5m };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithZeroPrice_Returns400BadRequest()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 0m,
            Quantity = 0.5m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithNegativePrice_Returns400BadRequest()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = -100m,
            Quantity = 0.5m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithZeroQuantity_Returns400BadRequest()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 50_000m,
            Quantity = 0m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithNegativeQuantity_Returns400BadRequest()
    {
        var request = new CreateOrderRequest
        {
            Symbol = "BTC/USD",
            Side = OrderSide.Buy,
            Price = 50_000m,
            Quantity = -1m
        };

        var response = await _client.PostAsJsonAsync("/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
