using Microsoft.EntityFrameworkCore;
using OrderMatcher.Api.Data;
using OrderMatcher.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<OrderMatcherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? "Data Source=ordermatcher.db"));
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderMatcherDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();

app.Run();

public partial class Program { }
