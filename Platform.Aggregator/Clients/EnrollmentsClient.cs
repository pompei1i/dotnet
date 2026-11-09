using Enrollments.Grpc;

namespace Platform.Aggregator.Clients;

/// <summary>Клієнт сервісу Enrollments (Лаб. 02) поверх згенерованого gRPC-клієнта: замовлення й продажі курсів.</summary>
public sealed class EnrollmentsClient(OrdersService.OrdersServiceClient grpc, ILogger<EnrollmentsClient> logger) : GrpcUpstream(logger)
{
    public override string ServiceName => "enrollments-api";

    public async Task<OrderResponse?> GetOrderAsync(long orderId, CancellationToken ct)
    {
        var order = await CallAsync("GetOrder", () => grpc.GetOrderAsync(new GetOrderRequest { Id = orderId }, Options(ct)));
        return order is null
            ? null
            : new OrderResponse(
                order.Id,
                order.StudentId,
                order.Status,
                order.OrderDate.ToDateTime(),
                order.TotalAmount.ToDecimal(),
                order.HasPaymentMethod ? new OrderDetailsResponse(order.PaymentMethod) : null,
                [.. order.Items.Select(item => new OrderItemResponse(
                    item.CourseId, item.CourseTitle, item.UnitPrice.ToDecimal(), item.Discount.ToDecimal(), item.LineTotal.ToDecimal()))]);
    }

    public async Task<CourseSalesResponse?> GetCourseSalesAsync(long courseId, CancellationToken ct)
    {
        var sales = await CallAsync("GetCourseSales",
            () => grpc.GetCourseSalesAsync(new GetCourseSalesRequest { CourseId = courseId }, Options(ct)));
        return sales is null ? null : new CourseSalesResponse(sales.CourseId, sales.OrdersCount, sales.Revenue.ToDecimal());
    }
}

// Дані замовлень, потрібні агрегатору (з protobuf-повідомлень)

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
