using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>
/// Aggregator Pattern — повна картка замовлення. Спершу замовлення (без нього агрегувати нічого),
/// потім одночасно (Task.WhenAll) каталог і відгуки для курсів цього замовлення. Якщо один із
/// сервісів недоступний, картка повертається з тим, що вдалося зібрати, і з попередженням у Warnings.
/// </summary>
public sealed class OrderCardService(
    EnrollmentsClient enrollments,
    CoursesClient courses,
    ReviewsClient reviews,
    ILogger<OrderCardService> logger)
{
    public async Task<OrderCardDto?> GetOrderCardAsync(long orderId, CancellationToken ct)
    {
        using var activity = AggregatorTelemetry.Source.StartActivity("BuildOrderCard");
        activity?.SetTag("order.id", orderId);

        var context = new AggregationContext(logger, ct);

        // 1. Обов'язкові дані: недоступний enrollments-api -> 503, неіснуюче замовлення -> 404
        var order = await context.RequiredAsync(enrollments.ServiceName, () => enrollments.GetOrderAsync(orderId, ct));
        if (order is null)
            return null;

        var courseIds = order.Items.Select(item => item.CourseId).Distinct().ToList();
        activity?.SetTag("order.courses_count", courseIds.Count);

        // 2. Незалежні виклики — паралельно: загальний час ≈ найповільніший сервіс, а не сума.
        //    CancellationToken іде в усі виклики: якщо клієнт скасував запит, зупиняються всі вихідні.
        var catalogTask = context.SafeAsync(courses.ServiceName, () => courses.GetCoursesAsync(courseIds, ct));
        var feedbackTask = context.SafeAsync(reviews.ServiceName, () => reviews.GetFeedbackAsync(courseIds, ct));

        await Task.WhenAll(catalogTask, feedbackTask);

        var catalog = await catalogTask;
        var feedback = await feedbackTask;

        // 3. Композиція з перевіркою узгодженості: до позиції потрапляють лише дані саме її курсу
        var items = order.Items
            .Select(item => BuildItem(item, catalog, feedback, context))
            .ToList();

        var card = new OrderCardDto(
            order.Id,
            order.StudentId,
            order.Status,
            order.OrderDate,
            order.TotalAmount,
            order.Details?.PaymentMethod,
            items,
            context.Sources,
            context.Warnings);

        activity?.SetTag("aggregation.warnings", card.Warnings.Count);
        logger.LogInformation("Order card {OrderId} built: {ItemsCount} item(s), {WarningsCount} warning(s)",
            order.Id, items.Count, card.Warnings.Count);

        return card;
    }

    private static OrderCardItemDto BuildItem(
        OrderItemResponse item,
        IReadOnlyDictionary<long, CourseResponse?>? catalog,
        IReadOnlyDictionary<long, CourseFeedbackResponse>? feedback,
        AggregationContext context)
    {
        // catalog == null — сервіс недоступний (попередження вже є); відсутній ключ або null — курсу немає
        var course = catalog?.GetValueOrDefault(item.CourseId);
        if (catalog is not null && course is null)
            context.Warn($"Course {item.CourseId} from the order is not found in courses-api");

        if (course is not null && course.Id != item.CourseId)
        {
            context.Warn($"courses-api returned course {course.Id} instead of {item.CourseId}; catalog data skipped");
            course = null;
        }

        var courseFeedback = feedback?.GetValueOrDefault(item.CourseId);
        if (courseFeedback?.Rating is { } rating && rating.CourseId != item.CourseId)
        {
            context.Warn($"reviews-api returned rating of course {rating.CourseId} instead of {item.CourseId}; feedback skipped");
            courseFeedback = null;
        }

        return new OrderCardItemDto(
            item.CourseId,
            item.CourseTitle,
            item.UnitPrice,
            item.Discount,
            item.LineTotal,
            PriceChanged: course is null ? null : course.Price != item.UnitPrice,
            Course: course?.ToCourseInfo(),
            Feedback: courseFeedback?.ToCourseFeedback());
    }
}
