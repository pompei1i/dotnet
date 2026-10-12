using System.Data;
using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Validation;
using Enrollments.Dal;
using Enrollments.Domain;
using Enrollments.Domain.Exceptions;
using Enrollments.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Enrollments.Bll.Services;

public class OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger) : IOrderService
{
    // Автентифікації ще немає, тому в аудитні колонки записується назва сервісу
    private const string Actor = "enrollments-api";

    public async Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var order = await uow.Orders.GetWithItemsAsync(id, ct)
                    ?? throw new NotFoundException($"Order {id} not found");

        return mapper.Map<OrderDto>(order);
    }

    public async Task<PagedResultDto<OrderSummaryDto>> GetByStudentAsync(
        long studentId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (status is not null)
            OrderRules.EnsureKnownStatus(status);

        _ = await uow.Students.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException($"Student {studentId} not found");

        var result = await uow.Orders.GetByStudentAsync(studentId, status, page, pageSize, ct);

        return new PagedResultDto<OrderSummaryDto>(
            mapper.Map<IReadOnlyList<OrderSummaryDto>>(result.Items), result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken ct = default)
    {
        _ = await uow.Orders.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Order {id} not found");

        var history = await uow.StatusHistory.GetByOrderIdAsync(id, ct);
        return mapper.Map<IReadOnlyList<OrderStatusHistoryDto>>(history);
    }

    /// <summary>
    /// Транзакційна бізнес-операція «оформити замовлення». В одній транзакції Unit of Work:
    /// читає студента (Students) і курси (Courses), пише замовлення, реквізити та позиції
    /// (Orders) і перший запис журналу (StatusHistory) — чотири таблиці через чотири репозиторії.
    /// Будь-яка помилка, наприклад недоступний курс у другій позиції, відкочує вже вставлені рядки.
    /// </summary>
    public async Task<OrderDto> CreateAsync(CreateOrderDto input, CancellationToken ct = default)
    {
        OrderRules.EnsureNoDuplicateCourses(input.Items);

        long orderId;

        // ReadCommitted достатньо: операція лише додає нові рядки і не змінює прочитані
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var student = await uow.Students.GetByIdAsync(input.StudentId, ct)
                          ?? throw new NotFoundException($"Student {input.StudentId} not found");

            var order = new Order
            {
                StudentId = student.Id,
                Status = OrderStatuses.Submitted,
                CreatedBy = Actor
            };
            orderId = await uow.Orders.AddAsync(order, ct);                       // dbo.Orders

            await uow.Orders.AddDetailsAsync(new OrderDetails                     // dbo.OrderDetails
            {
                OrderId = orderId,
                BillingName = input.BillingName,
                BillingEmail = input.BillingEmail,
                PaymentMethod = input.PaymentMethod,
                Comment = input.Comment,
                CreatedBy = Actor
            }, ct);

            var total = 0m;
            foreach (var line in input.Items)
                total += await AddItemSnapshotAsync(orderId, line, ct);           // dbo.OrderItems

            await uow.Orders.UpdateTotalAsync(orderId, total, Actor, ct);

            await uow.StatusHistory.AddAsync(new OrderStatusHistoryEntry          // dbo.OrderStatusHistory
            {
                OrderId = orderId,
                OldStatus = null,
                NewStatus = OrderStatuses.Submitted,
                ChangedBy = Actor,
                Comment = "Order created"
            }, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            // Відкат не можна скасовувати разом із запитом, тому тут CancellationToken.None
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation("Order {OrderId} created for student {StudentId} with {ItemsCount} course(s)",
            orderId, input.StudentId, input.Items.Count);

        return await GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Транзакційна бізнес-операція «додати курс до замовлення»: вставка позиції та оновлення
    /// суми замовлення виконуються разом або не виконуються взагалі.
    /// </summary>
    public async Task<OrderDto> AddItemAsync(long orderId, CreateOrderItemDto input, CancellationToken ct = default)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Рядок замовлення блокується до кінця транзакції (UPDLOCK): паралельний запит
            // не відправить замовлення на оплату між перевіркою статусу і вставкою позиції.
            // Це точковіше, ніж піднімати рівень ізоляції всієї транзакції до Serializable.
            _ = await uow.Orders.GetForUpdateAsync(orderId, ct)
                ?? throw new NotFoundException($"Order {orderId} not found");

            var order = await uow.Orders.GetWithItemsAsync(orderId, ct)
                        ?? throw new NotFoundException($"Order {orderId} not found");

            OrderRules.EnsureEditable(order);

            if (order.Items.Any(item => item.CourseId == input.CourseId))
                throw new BusinessConflictException($"Course {input.CourseId} is already in order {orderId}");

            var lineTotal = await AddItemSnapshotAsync(orderId, input, ct);
            var total = order.Items.Sum(item => item.LineTotal) + lineTotal;

            await uow.Orders.UpdateTotalAsync(orderId, total, Actor, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }

        logger.LogInformation("Course {CourseId} added to order {OrderId}", input.CourseId, orderId);
        return await GetByIdAsync(orderId, ct);
    }

    public async Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default)
    {
        OrderRules.EnsureKnownStatus(newStatus);

        // Допустимість переходу перевіряє процедура usp_ChangeOrderStatus з Лаб. 01; вона сама
        // працює в транзакції, тому окрема транзакція Unit of Work тут не потрібна.
        // DAL перетворює її помилки: 50001 -> NotFoundException, 50003 -> BusinessConflictException.
        await uow.Orders.ChangeStatusAsync(orderId, newStatus, Actor, comment: null, ct: ct);

        logger.LogInformation("Order {OrderId} moved to status {Status}", orderId, newStatus);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        // Процедура usp_DeleteOrder дозволяє видалити лише чернетку або скасоване замовлення
        if (!await uow.Orders.DeleteAsync(id, ct))
            throw new NotFoundException($"Order {id} not found");

        logger.LogInformation("Order {OrderId} deleted", id);
    }

    /// <summary>
    /// Додає позицію зі ЗНІМКОМ назви та ціни курсу: значення беруться з локального довідника
    /// dbo.Courses у момент оформлення й надалі не оновлюються (політика дублювання з Лаб. 01).
    /// </summary>
    /// <returns>Сума позиції з урахуванням знижки.</returns>
    private async Task<decimal> AddItemSnapshotAsync(long orderId, CreateOrderItemDto line, CancellationToken ct)
    {
        var course = await uow.Courses.GetByIdAsync(line.CourseId, ct)
                     ?? throw new NotFoundException($"Course {line.CourseId} not found");

        OrderRules.EnsureCourseAvailable(course);
        OrderRules.EnsureDiscountWithinPrice(line.Discount, course);

        var item = new OrderItem
        {
            OrderId = orderId,
            CourseId = course.Id,
            CourseTitle = course.Title,   // знімок назви
            UnitPrice = course.Price,     // знімок ціни
            Discount = line.Discount,
            CreatedBy = Actor
        };
        await uow.Orders.AddItemAsync(item, ct);

        return item.LineTotal;
    }
}
