using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Data;
using OrderMatcher.Api.DTOs;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Services;

public class OrderService : IOrderService
{
    private readonly OrderMatcherDbContext _db;

    public OrderService(OrderMatcherDbContext db)
    {
        _db = db;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Symbol = request.Symbol,
            Side = request.Side,
            Price = request.Price,
            Quantity = request.Quantity,
            FilledQuantity = 0m,
            Status = OrderStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return new OrderResponse
        {
            Id = order.Id,
            Symbol = order.Symbol,
            Side = order.Side,
            Price = order.Price,
            Quantity = order.Quantity,
            FilledQuantity = order.FilledQuantity,
            Status = order.Status,
            CreatedAt = order.CreatedAt
        };
    }

    public async Task<IEnumerable<Order>> FindMatchesAsync(Order incomingOrder)
    {
        var eligibleStatuses = new[] { OrderStatus.Open, OrderStatus.PartiallyFilled };

        if (incomingOrder.Side == OrderSide.Buy)
        {
            return await _db.Orders
                .Where(o => o.Symbol == incomingOrder.Symbol
                         && o.Side == OrderSide.Sell
                         && eligibleStatuses.Contains(o.Status)
                         && o.Price <= incomingOrder.Price)
                .OrderBy(o => o.Price)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync();
        }
        else
        {
            return await _db.Orders
                .Where(o => o.Symbol == incomingOrder.Symbol
                         && o.Side == OrderSide.Buy
                         && eligibleStatuses.Contains(o.Status)
                         && o.Price >= incomingOrder.Price)
                .OrderByDescending(o => o.Price)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync();
        }
    }

    public async Task<IEnumerable<Trade>> MatchAndSettleAsync(Order incomingOrder)
    {
        var trades = new List<Trade>();
        var matches = await FindMatchesAsync(incomingOrder);

        foreach (var bookOrder in matches)
        {
            if (incomingOrder.FilledQuantity >= incomingOrder.Quantity)
                break;

            var incomingRemaining = incomingOrder.Quantity - incomingOrder.FilledQuantity;
            var bookRemaining = bookOrder.Quantity - bookOrder.FilledQuantity;
            var fillQty = Math.Min(incomingRemaining, bookRemaining);

            var trade = new Trade
            {
                Id = Guid.NewGuid(),
                Symbol = incomingOrder.Symbol,
                BuyOrderId = incomingOrder.Side == OrderSide.Buy ? incomingOrder.Id : bookOrder.Id,
                SellOrderId = incomingOrder.Side == OrderSide.Sell ? incomingOrder.Id : bookOrder.Id,
                Price = bookOrder.Price,
                Quantity = fillQty,
                ExecutedAt = DateTime.UtcNow
            };

            trades.Add(trade);
            _db.Trades.Add(trade);

            bookOrder.FilledQuantity += fillQty;
            bookOrder.Status = bookOrder.FilledQuantity >= bookOrder.Quantity
                ? OrderStatus.Filled
                : OrderStatus.PartiallyFilled;

            incomingOrder.FilledQuantity += fillQty;
            incomingOrder.Status = incomingOrder.FilledQuantity >= incomingOrder.Quantity
                ? OrderStatus.Filled
                : OrderStatus.PartiallyFilled;
        }

        await _db.SaveChangesAsync();
        return trades;
    }

    public Task<OrderBookResponse> GetOrderBookAsync(string symbol)
    {
        throw new NotImplementedException();
    }
}

