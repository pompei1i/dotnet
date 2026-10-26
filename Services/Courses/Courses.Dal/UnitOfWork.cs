using Courses.Dal.Interfaces;
using Courses.Dal.Repositories;
using Courses.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal;

/// <summary>
/// Unit of Work на EF Core. Репозиторії лише додають, змінюють і видаляють сутності в спільному
/// контексті; SaveChangesAsync викликається один раз наприкінці бізнес-операції, і EF Core
/// записує всі зміни в одній транзакції. Помилки бази перетворюються на доменні винятки,
/// щоб шари вище не залежали від EF Core і SqlClient.
/// </summary>
public sealed class UnitOfWork(CoursesDbContext context) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ReferenceConstraintViolation = 547;

    public ICourseRepository Courses { get; } = new CourseRepository(context);
    public ICategoryRepository Categories { get; } = new CategoryRepository(context);
    public ICourseCategoryRepository CourseCategories { get; } = new CourseCategoryRepository(context);
    public IInstructorRepository Instructors { get; } = new InstructorRepository(context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // RowVersion у базі вже інший: рядок змінив хтось інший після нашого читання
            throw new ConflictException("The record was changed by another user. Reload it and try again", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("A record with the same unique value already exists", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: ReferenceConstraintViolation })
        {
            throw new ConflictException("The operation conflicts with related records", ex);
        }
    }
}
