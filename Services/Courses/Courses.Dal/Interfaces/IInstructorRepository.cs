using Courses.Domain.Entities;

namespace Courses.Dal.Interfaces;

public interface IInstructorRepository : IGenericRepository<Instructor>
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
}
