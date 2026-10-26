using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Bll.Specifications;
using Courses.Dal;
using Courses.Domain.Entities;
using Courses.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Courses.Bll.Services;

public class InstructorService(IUnitOfWork uow, IMapper mapper, ILogger<InstructorService> logger) : IInstructorService
{
    public async Task<IReadOnlyList<InstructorDto>> GetAllAsync(CancellationToken ct = default)
    {
        var instructors = await uow.Instructors.GetAllAsync(ct);
        return mapper.Map<IReadOnlyList<InstructorDto>>(instructors.OrderBy(instructor => instructor.FullName));
    }

    public async Task<InstructorDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var instructor = await uow.Instructors.GetByIdAsync(id, ct)
                         ?? throw new EntityNotFoundException(nameof(Instructor), id);

        return mapper.Map<InstructorDto>(instructor);
    }

    public async Task<InstructorDto> CreateAsync(CreateInstructorDto input, CancellationToken ct = default)
    {
        if (await uow.Instructors.ExistsByEmailAsync(input.Email, ct))
            throw new ConflictException($"Instructor with email '{input.Email}' already exists");

        var instructor = new Instructor(input.Email, input.FullName, input.Bio);
        await uow.Instructors.AddAsync(instructor, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Instructor {InstructorId} created", instructor.Id);
        return mapper.Map<InstructorDto>(instructor);
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(long id, bool publishedOnly, CancellationToken ct = default)
    {
        _ = await uow.Instructors.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Instructor), id);

        var courses = await uow.Courses.ListAsync(new InstructorCoursesSpec(id, publishedOnly), ct);
        return mapper.Map<IReadOnlyList<CourseDto>>(courses);
    }
}
