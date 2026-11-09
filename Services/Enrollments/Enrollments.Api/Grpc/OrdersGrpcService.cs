using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Domain.Exceptions;
using Enrollments.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Enrollments.Api.Grpc;

/// <summary>
/// gRPC-адаптер над BLL замовлень, як і контролери: перевіряє вхідні дані, викликає IOrderService /
/// ICourseService, перетворює DTO на protobuf-повідомлення, а доменні винятки — на RpcException
/// (NotFound, InvalidArgument, FailedPrecondition). Решту GrpcLoggingInterceptor повертає як Internal.
/// </summary>
public sealed class OrdersGrpcService(IOrderService orders, ICourseService courses) : OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            return ToReply(await orders.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    public override async Task<CourseSalesReply> GetCourseSales(GetCourseSalesRequest request, ServerCallContext context)
    {
        if (request.CourseId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "course_id must be positive"));

        try
        {
            var sales = await courses.GetSalesAsync(request.CourseId, context.CancellationToken);
            return new CourseSalesReply
            {
                CourseId = sales.CourseId,
                CourseTitle = sales.CourseTitle,
                OrdersCount = sales.OrdersCount,
                Revenue = DecimalValue.FromDecimal(sales.Revenue)
            };
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    private static Status? ToStatus(Exception exception) => exception switch
    {
        NotFoundException => new Status(StatusCode.NotFound, exception.Message),
        ValidationException => new Status(StatusCode.InvalidArgument, exception.Message),
        BusinessConflictException => new Status(StatusCode.FailedPrecondition, exception.Message),
        _ => null
    };

    private static OrderReply ToReply(OrderDto order)
    {
        var reply = new OrderReply
        {
            Id = order.Id,
            StudentId = order.StudentId,
            Status = order.Status,
            OrderDate = Timestamp.FromDateTime(DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc)),
            TotalAmount = DecimalValue.FromDecimal(order.TotalAmount),
            Items =
            {
                order.Items.Select(item => new OrderItemReply
                {
                    CourseId = item.CourseId,
                    CourseTitle = item.CourseTitle,
                    UnitPrice = DecimalValue.FromDecimal(item.UnitPrice),
                    Discount = DecimalValue.FromDecimal(item.Discount),
                    LineTotal = DecimalValue.FromDecimal(item.LineTotal)
                })
            }
        };

        if (order.Details?.PaymentMethod is { } paymentMethod)
            reply.PaymentMethod = paymentMethod;

        return reply;
    }
}
