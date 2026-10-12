using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Dal;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain.Exceptions;
using Enrollments.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Enrollments.Tests;

public class StudentServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly StudentService _service;

    public StudentServiceTests()
    {
        _uow.Students.Returns(_students);
        _uow.Orders.Returns(_orders);

        _students.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Student { Id = 1, Email = "olena@example.com", FullName = "Olena", RowVersion = [0, 0, 0, 0, 0, 0, 0, 1] });

        _service = new StudentService(_uow, MappingTests.CreateMapper(), NullLogger<StudentService>.Instance);
    }

    [Fact]
    public async Task Create_with_existing_email_throws_conflict_and_does_not_insert()
    {
        _students.GetByEmailAsync("olena@example.com", Arg.Any<CancellationToken>())
            .Returns(new Student { Id = 1, Email = "olena@example.com" });

        await Assert.ThrowsAsync<BusinessConflictException>(
            () => _service.CreateAsync(new CreateStudentDto { Email = "olena@example.com", FullName = "Another" }));

        await _students.DidNotReceive().AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_inserts_student_and_returns_it()
    {
        _students.AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>()).Returns(1L);

        var created = await _service.CreateAsync(new CreateStudentDto { Email = "olena@example.com", FullName = "Olena" });

        Assert.Equal(1, created.Id);
        await _students.Received(1).AddAsync(
            Arg.Is<Student>(student => student.Email == "olena@example.com" && student.CreatedBy != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_stale_row_version_throws_conflict()
    {
        _students.UpdateAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>()).Returns(false);

        var input = new UpdateStudentDto { Email = "olena@example.com", FullName = "Olena K.", RowVersion = [9, 9, 9, 9, 9, 9, 9, 9] };

        await Assert.ThrowsAsync<BusinessConflictException>(() => _service.UpdateAsync(1, input));
    }

    [Fact]
    public async Task Update_passes_id_and_row_version_from_request_to_repository()
    {
        _students.UpdateAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>()).Returns(true);
        byte[] rowVersion = [0, 0, 0, 0, 0, 0, 0, 1];

        await _service.UpdateAsync(1, new UpdateStudentDto { Email = "olena@example.com", FullName = "Olena K.", RowVersion = rowVersion });

        await _students.Received(1).UpdateAsync(
            Arg.Is<Student>(student => student.Id == 1 && student.FullName == "Olena K." && student.RowVersion.SequenceEqual(rowVersion)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_student_with_unfinished_orders_throws_conflict()
    {
        _orders.CountActiveByStudentAsync(1, Arg.Any<CancellationToken>()).Returns(2);

        await Assert.ThrowsAsync<BusinessConflictException>(() => _service.DeleteAsync(1));

        await _students.DidNotReceive().DeleteAsync(Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_missing_student_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
    }
}
