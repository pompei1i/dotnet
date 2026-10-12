using Enrollments.Bll.Dtos;
using Enrollments.Domain;
using Enrollments.Domain.Exceptions;
using Enrollments.Domain.Models;

namespace Enrollments.Bll.Validation;

/// <summary>
/// Бізнес-правила замовлення — другий рівень валідації після DataAnnotations на DTO.
/// Атрибути перевіряють форму запиту (обов'язковість, довжину, діапазон) ще до виклику сервісу;
/// ці правила перевіряють зміст і стан даних, прочитаних із бази.
/// </summary>
public static class OrderRules
{
    /// <summary>Один курс не може входити в замовлення двічі.</summary>
    public static void EnsureNoDuplicateCourses(IEnumerable<CreateOrderItemDto> items)
    {
        var duplicates = items
            .GroupBy(item => item.CourseId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new ValidationException(
                $"Each course can be added to an order only once. Repeated course ids: {string.Join(", ", duplicates)}");
    }

    /// <summary>Купити можна лише курс, опублікований у каталозі.</summary>
    public static void EnsureCourseAvailable(Course course)
    {
        if (!course.IsAvailable)
            throw new BusinessConflictException($"Course {course.Id} is not available for purchase");
    }

    /// <summary>Знижка не може перевищувати ціну курсу — ціну знає лише база.</summary>
    public static void EnsureDiscountWithinPrice(decimal discount, Course course)
    {
        if (discount > course.Price)
            throw new ValidationException(
                $"Discount {discount} exceeds the price {course.Price} of course {course.Id}");
    }

    /// <summary>Склад замовлення можна змінювати лише до відправки на оплату.</summary>
    public static void EnsureEditable(Order order)
    {
        if (order.Status != OrderStatuses.Submitted)
            throw new BusinessConflictException(
                $"Order {order.Id} is in status {order.Status}; courses can be added only in status {OrderStatuses.Submitted}");
    }

    public static void EnsureKnownStatus(string status)
    {
        if (!OrderStatuses.All.Contains(status))
            throw new ValidationException(
                $"Unknown order status '{status}'. Allowed: {string.Join(", ", OrderStatuses.All)}");
    }
}
