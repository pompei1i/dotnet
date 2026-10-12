using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Dal;
using Enrollments.Domain.Exceptions;
using Enrollments.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Enrollments.Bll.Services;

public class StudentService(IUnitOfWork uow, IMapper mapper, ILogger<StudentService> logger) : IStudentService
{
    // Автентифікації ще немає, тому в аудитні колонки записується назва сервісу
    private const string Actor = "enrollments-api";

    public async Task<StudentDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var student = await uow.Students.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Student {id} not found");

        return mapper.Map<StudentDto>(student);
    }

    public async Task<PagedResultDto<StudentDto>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var result = await uow.Students.GetPagedAsync(NormalizeSearch(search), page, pageSize, ct);

        return new PagedResultDto<StudentDto>(
            mapper.Map<IReadOnlyList<StudentDto>>(result.Items), result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<StudentDto> CreateAsync(CreateStudentDto input, CancellationToken ct = default)
    {
        // Бізнес-правило, для якого потрібен стан бази: Email унікальний серед студентів.
        // Від гонки двох одночасних запитів додатково захищає UQ_Students_Email у базі.
        if (await uow.Students.GetByEmailAsync(input.Email, ct) is not null)
            throw new BusinessConflictException($"Student with email '{input.Email}' already exists");

        var student = mapper.Map<Student>(input);
        student.CreatedBy = Actor;

        var id = await uow.Students.AddAsync(student, ct);
        logger.LogInformation("Student {StudentId} created", id);

        return await GetByIdAsync(id, ct);
    }

    public async Task<StudentDto> UpdateAsync(long id, UpdateStudentDto input, CancellationToken ct = default)
    {
        var existing = await uow.Students.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Student {id} not found");

        if (!string.Equals(existing.Email, input.Email, StringComparison.OrdinalIgnoreCase)
            && await uow.Students.GetByEmailAsync(input.Email, ct) is not null)
            throw new BusinessConflictException($"Student with email '{input.Email}' already exists");

        var student = mapper.Map<Student>(input);   // Email, FullName, Phone і RowVersion із запиту
        student.Id = id;
        student.UpdatedBy = Actor;

        // Оптимістична конкурентність: якщо RowVersion із запиту застарів, оновлено 0 рядків
        if (!await uow.Students.UpdateAsync(student, ct))
            throw new BusinessConflictException(
                $"Student {id} was changed by another user. Reload it and try again");

        logger.LogInformation("Student {StudentId} updated", id);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _ = await uow.Students.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Student {id} not found");

        // Студента з незавершеними замовленнями видаляти не можна
        var activeOrders = await uow.Orders.CountActiveByStudentAsync(id, ct);
        if (activeOrders > 0)
            throw new BusinessConflictException(
                $"Student {id} has {activeOrders} unfinished order(s) and cannot be deleted");

        if (!await uow.Students.DeleteAsync(id, Actor, ct))
            throw new NotFoundException($"Student {id} not found");

        logger.LogInformation("Student {StudentId} deleted", id);
    }

    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim();
}
