using System.ComponentModel.DataAnnotations;

// TODO 1: Strongly-typed options class with validation
public class PaymentOptions
{
    [Required]
    public required string GatewayUrl { get; init; }
    
    [Range(100, 100000)]
    public decimal MaxDepositBirr { get; init; }
}