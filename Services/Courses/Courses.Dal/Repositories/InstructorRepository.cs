using Courses.Dal.Interfaces;
using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal.Repositories;

public class InstructorRepository(CoursesDbContext context)
    : GenericRepository<Instructor>(context), IInstructorRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => Set.AnyAsync(instructor => instructor.Email == email, ct);
}
