using System.Data;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Dal;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain;
using Enrollments.Domain.Exceptions;
using Enrollments.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Enrollments.Tests;

/// <summary>Unit-тести BLL: DAL і Unit of Work замінено підставними об'єктами.</summary>
public class OrderServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly IOrderStatusHistoryRepository _history = Substitute.For<IOrderStatusHistoryRepository>();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _uow.Students.Returns(_students);
        _uow.Orders.Returns(_orders);
        _uow.Courses.Returns(_courses);
        _uow.StatusHistory.Returns(_history);

        _students.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Student { Id = 1, Email = "olena@example.com", FullName = "Olena" });
        _courses.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Course { Id = 1, Title = "C# for beginners", Price = 1200m, IsAvailable = true });
        _courses.GetByIdAsync(6, Arg.Any<CancellationToken>())
            .Returns(new Course { Id = 6, Title = "Docker", Price = 2000m, IsAvailable = false });
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(42L);
        _orders.GetWithItemsAsync(42, Arg.Any<CancellationToken>())
            .Returns(new Order { Id = 42, StudentId = 1, Status = OrderStatuses.Submitted });

        _service = new OrderService(_uow, MappingTests.CreateMapper(), NullLogger<OrderService>.Instance);
    }

    private static CreateOrderDto NewOrder(params CreateOrderItemDto[] items) => new()
    {
        StudentId = 1,
        BillingName = "Olena Koval",
        BillingEmail = "olena@example.com",
        PaymentMethod = "Card",
        Items = [.. items]
    };

    [Fact]
    public async Task Create_writes_item_with_snapshot_of_course_title_and_price_and_commits()
    {
        var result = await _service.CreateAsync(NewOrder(new CreateOrderItemDto { CourseId = 1, Discount = 200m }));

        Assert.Equal(42, result.Id);
        await _uow.Received(1).BeginTransactionAsync(IsolationLevel.ReadCommitted, Arg.Any<CancellationToken>());
        await _orders.Received(1).AddItemAsync(
            Arg.Is<OrderItem>(item =>
                item.OrderId == 42 && item.CourseId == 1 &&
                item.CourseTitle == "C# for beginners" && item.UnitPrice == 1200m && item.Discount == 200m),
            Arg.Any<CancellationToken>());
        await _orders.Received(1).UpdateTotalAsync(42, 1000m, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _history.Received(1).AddAsync(
            Arg.Is<OrderStatusHistoryEntry>(entry => entry.OrderId == 42 && entry.NewStatus == OrderStatuses.Submitted),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rolls_back_inserted_order_when_second_course_is_unavailable()
    {
        var input = NewOrder(
            new CreateOrderItemDto { CourseId = 1 },
            new CreateOrderItemDto { CourseId = 6 });

        await Assert.ThrowsAsync<BusinessConflictException>(() => _service.CreateAsync(input));

        await _orders.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());   // замовлення вже вставлено
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());                     // і відкочено
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_for_unknown_student_throws_not_found_and_rolls_back()
    {
        var input = NewOrder(new CreateOrderItemDto { CourseId = 1 });
        input.StudentId = 999;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(input));

        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_repeated_course_throws_validation_before_transaction_starts()
    {
        var input = NewOrder(
            new CreateOrderItemDto { CourseId = 1 },
            new CreateOrderItemDto { CourseId = 1 });

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(input));

        await _uow.DidNotReceive().BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_discount_above_price_throws_validation_and_rolls_back()
    {
        var input = NewOrder(new CreateOrderItemDto { CourseId = 1, Discount = 5000m });

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(input));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItem_to_order_that_is_not_submitted_throws_conflict_and_rolls_back()
    {
        var paid = new Order { Id = 5, StudentId = 1, Status = OrderStatuses.Paid };
        _orders.GetForUpdateAsync(5, Arg.Any<CancellationToken>()).Returns(paid);
        _orders.GetWithItemsAsync(5, Arg.Any<CancellationToken>()).Returns(paid);

        await Assert.ThrowsAsync<BusinessConflictException>(
            () => _service.AddItemAsync(5, new CreateOrderItemDto { CourseId = 1 }));

        await _orders.DidNotReceive().AddItemAsync(Arg.Any<OrderItem>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItem_adds_line_and_recalculates_total()
    {
        var order = new Order { Id = 42, StudentId = 1, Status = OrderStatuses.Submitted };
        order.Items.Add(new OrderItem { OrderId = 42, CourseId = 3, CourseTitle = "EF Core", UnitPrice = 1500m });
        _orders.GetForUpdateAsync(42, Arg.Any<CancellationToken>()).Returns(order);
        _orders.GetWithItemsAsync(42, Arg.Any<CancellationToken>()).Returns(order);

        await _service.AddItemAsync(42, new CreateOrderItemDto { CourseId = 1, Discount = 200m });

        await _orders.Received(1).UpdateTotalAsync(42, 2500m, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItem_with_course_already_in_order_throws_conflict()
    {
        var order = new Order { Id = 42, StudentId = 1, Status = OrderStatuses.Submitted };
        order.Items.Add(new OrderItem { OrderId = 42, CourseId = 1, CourseTitle = "C# for beginners", UnitPrice = 1200m });
        _orders.GetForUpdateAsync(42, Arg.Any<CancellationToken>()).Returns(order);
        _orders.GetWithItemsAsync(42, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<BusinessConflictException>(
            () => _service.AddItemAsync(42, new CreateOrderItemDto { CourseId = 1 }));
    }

    [Fact]
    public async Task GetById_for_missing_order_throws_not_found()
    {
        _orders.GetWithItemsAsync(999, Arg.Any<CancellationToken>()).Returns((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task ChangeStatus_with_unknown_status_throws_validation_without_calling_dal()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ChangeStatusAsync(1, "Shipped"));

        await _orders.DidNotReceiveWithAnyArgs().ChangeStatusAsync(default, default!, default, default);
    }

    [Fact]
    public async Task ChangeStatus_passes_new_status_to_stored_procedure_call()
    {
        await _service.ChangeStatusAsync(3, OrderStatuses.AwaitingPayment);

        await _orders.Received(1).ChangeStatusAsync(
            3, OrderStatuses.AwaitingPayment, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_for_missing_order_throws_not_found()
    {
        _orders.DeleteAsync(999, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
    }
}
