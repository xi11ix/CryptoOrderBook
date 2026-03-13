# OrderMatcher

A crypto order matching REST API built with ASP.NET Core 8 and SQLite. Supports placing limit orders, automatic matching and settlement, and order book queries.

## What it does

- **Place limit orders** — buy or sell a crypto symbol at a specified price and quantity
- **Automatic matching** — when an order is created, it is immediately matched against resting orders on the opposite side using price-time priority
- **Settlement** — matched orders produce `Trade` records and both sides have their filled quantity and status updated atomically
- **Order book** — query aggregated bid/ask levels for any symbol
- **Order lookup** — retrieve any order by ID

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/orders` | Place a new limit order |
| `GET` | `/orders/order/{id}` | Get an order by ID |
| `GET` | `/orders/orderbook/{symbol}` | Get the order book for a symbol |

## How to run

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

### Start the API

```bash
cd OrderMatcher.Api
dotnet run
```

The API starts on `http://localhost:5272` by default. A SQLite database file (`ordermatcher.db`) is created automatically on first run.

### Example usage

**Place a sell order:**
```bash
curl -X POST http://localhost:5272/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSD","side":1,"price":50000,"quantity":1.5}'
```

**Place a matching buy order:**
```bash
curl -X POST http://localhost:5272/orders \
  -H "Content-Type: application/json" \
  -d '{"symbol":"BTCUSD","side":0,"price":50000,"quantity":1.0}'
```

**Get the order by ID:**
```bash
curl http://localhost:5272/orders/order/<id>
```

**Get the order book:**
```bash
curl http://localhost:5272/orders/orderbook/BTCUSD
```

`side`: `0` = Buy, `1` = Sell

## How to run tests

```bash
cd OrderMatcher.Tests
dotnet test
```

Or from the solution root:

```bash
dotnet test
```

The test suite has 73 tests across 6 files covering all service methods and HTTP endpoints.

| Test file | Coverage |
|-----------|----------|
| `CreateOrderTests.cs` | HTTP integration: validation, 201 response, settlement on create |
| `CreateOrderSettlementTests.cs` | Settlement triggered by `CreateOrderAsync` |
| `FindMatchesTests.cs` | Price-time priority matching logic |
| `MatchAndSettleTests.cs` | Fill quantities, partial fills, status transitions, trade records |
| `GetOrderBookTests.cs` | Bid/ask aggregation, ordering, multi-level books |
| `GetOrderTests.cs` | Lookup by ID, 404 for unknown orders |

## Major design decisions

### TDD throughout

Every feature was written test-first: a failing test suite was written before any implementation, then the minimum code needed to make the tests pass was added. This ensured the API contract was defined before the logic.

### SQLite Database

Orders and trades are persisted to a SQLite file (`ordermatcher.db`) via EF Core 8. The schema is created automatically at startup with `EnsureCreated()`. SQLite was chosen for simplicity — no external database process is required.

### Price-time priority matching

When an incoming order is matched, resting orders on the opposite side are prioritised first by the most favourable price, then by the earliest creation time (FIFO). Buy orders match against the lowest-priced asks; sell orders match against the highest-priced bids.

### Trade price = book order price

Trades are executed at the resting (book) order's price, not the incoming order's price. This is the standard limit-order fill convention — the passive side sets the price, the aggressive side crosses it.

### Atomic settlement on order creation

`CreateOrderAsync` persists the new order and immediately calls `MatchAndSettleAsync`, which fills as many matches as possible and calls `SaveChangesAsync` once to write all trade records and status updates in a single database round-trip. The `OrderResponse` returned by `POST /orders` reflects the post-settlement state.

## Improvements

### Use A Different Database

I would like to change the database from a local SQLite to database hosted on an external server.

### Add An Order Queue

I'd like to implement an order queue where each order is added to a queue and then processed. This would improve performance when submitting new orders.
