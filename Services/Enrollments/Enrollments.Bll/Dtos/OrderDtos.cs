using System.ComponentModel.DataAnnotations;

namespace Enrollments.Bll.Dtos;

public record OrderItemDto(long CourseId, string CourseTitle, decimal UnitPrice, decimal Discount, decimal LineTotal);

public record OrderDetailsDto(string BillingName, string BillingEmail, string PaymentMethod, string? Comment);

public record OrderDto(
    long Id,
    long StudentId,
    string Status,
    DateTime OrderDate,
    decimal TotalAmount,
    OrderDetailsDto? Details,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>Рядок списку замовлень студента.</summary>
public record OrderSummaryDto(long Id, string Status, DateTime OrderDate, decimal TotalAmount);

public record OrderStatusHistoryDto(
    string? OldStatus,
    string NewStatus,
    DateTime ChangedAt,
    string? ChangedBy,
    string? Comment);

public class CreateOrderDto
{
    [Range(1, long.MaxValue)]
    public long StudentId { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string BillingName { get; set; } = "";

    [Required, EmailAddress, StringLength(256)]
    public string BillingEmail { get; set; } = "";

    [Required, RegularExpression("^(Card|PayPal|BankTransfer)$",
        ErrorMessage = "PaymentMethod must be Card, PayPal or BankTransfer")]
    public string PaymentMethod { get; set; } = "Card";

    [StringLength(1000)]
    public string? Comment { get; set; }

    [Required, MinLength(1), MaxLength(20)]
    public List<CreateOrderItemDto> Items { get; set; } = [];
}

public class CreateOrderItemDto
{
    [Range(1, long.MaxValue)]
    public long CourseId { get; set; }

    [Range(0, 1_000_000)]
    public decimal Discount { get; set; }
}
