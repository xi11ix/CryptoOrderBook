using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.Data;

public class OrderMatcherDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Trade> Trades { get; set; } = null!;

    public OrderMatcherDbContext(DbContextOptions<OrderMatcherDbContext> options) : base(options) { }
}
