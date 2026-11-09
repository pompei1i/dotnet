namespace Platform.Aggregator.Clients;

/// <summary>Typed-клієнт сервісу Enrollments (Лаб. 02): замовлення й продажі курсів.</summary>
public sealed class EnrollmentsClient(HttpClient http, ILogger<EnrollmentsClient> logger) : UpstreamClient(http, logger)
{
    public override string ServiceName => "enrollments-api";

    public Task<OrderResponse?> GetOrderAsync(long orderId, CancellationToken ct)
        => GetAsync<OrderResponse>($"/api/orders/{orderId}", ct);

    public Task<CourseSalesResponse?> GetCourseSalesAsync(long courseId, CancellationToken ct)
        => GetAsync<CourseSalesResponse>($"/api/courses/{courseId}/sales", ct);
}

// Відповіді enrollments-api — лише поля, потрібні агрегатору

public sealed record OrderResponse(
    long Id,
    long StudentId,
    string Status,
    DateTime OrderDate,
    decimal TotalAmount,
    OrderDetailsResponse? Details,
    IReadOnlyList<OrderItemResponse> Items);

public sealed record OrderDetailsResponse(string PaymentMethod);

public sealed record OrderItemResponse(long CourseId, string CourseTitle, decimal UnitPrice, decimal Discount, decimal LineTotal);

public sealed record CourseSalesResponse(long CourseId, int OrdersCount, decimal Revenue);
