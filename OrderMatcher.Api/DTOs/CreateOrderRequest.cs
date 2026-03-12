using System.ComponentModel.DataAnnotations;
using OrderMatcher.Api.Models;

namespace OrderMatcher.Api.DTOs;

public class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    public OrderSide Side { get; set; }

    [Range(0.00000001, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public decimal Price { get; set; }

    [Range(0.00000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }
}
