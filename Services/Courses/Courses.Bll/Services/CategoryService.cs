using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Dal;
using Courses.Domain.Entities;
using Courses.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Courses.Bll.Services;

public class CategoryService(IUnitOfWork uow, IMapper mapper, ILogger<CategoryService> logger) : ICategoryService
{
    /// <summary>Якщо дату не вказано — курси, додані за останні 90 днів.</summary>
    private static readonly TimeSpan DefaultRecentPeriod = TimeSpan.FromDays(90);

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await uow.Categories.GetAllAsync(ct);
        var counts = (await uow.CourseCategories.CountCoursesPerCategoryAsync(ct))
            .ToDictionary(count => count.CategoryId, count => count.CoursesCount);

        return categories
            .OrderBy(category => category.Name)
            .Select(category => new CategoryDto(
                category.Id, category.Name, category.Slug, category.Description,
                counts.GetValueOrDefault(category.Id)))
            .ToList();
    }

    public async Task<CategoryWithCoursesDto> GetWithCoursesAsync(int id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetWithCoursesAsync(id, ct)
                       ?? throw new EntityNotFoundException(nameof(Category), id);

        return mapper.Map<CategoryWithCoursesDto>(category);
    }

    public async Task<IReadOnlyList<RecentCourseDto>> GetRecentlyAddedAsync(
        string slug, DateTime? since, CancellationToken ct = default)
    {
        _ = await uow.Categories.GetBySlugAsync(slug, ct)
            ?? throw new EntityNotFoundException(nameof(Category), slug);

        var entries = await uow.CourseCategories.GetRecentlyAddedAsync(
            slug, since ?? DateTime.UtcNow - DefaultRecentPeriod, ct);

        return mapper.Map<IReadOnlyList<RecentCourseDto>>(entries);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default)
    {
        if (await uow.Categories.ExistsByNameOrSlugAsync(input.Name, input.Slug, ct))
            throw new ConflictException($"Category with name '{input.Name}' or slug '{input.Slug}' already exists");

        var category = new Category(input.Name, input.Slug, input.Description);
        await uow.Categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Category {CategoryId} '{CategorySlug}' created", category.Id, category.Slug);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.Description, CoursesCount: 0);
    }
}
