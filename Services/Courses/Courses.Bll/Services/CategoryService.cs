using AutoMapper;
using Courses.Bll.Caching;
using Courses.Bll.Dtos;
using Courses.Dal;
using Courses.Domain.Entities;
using Courses.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Courses.Bll.Services;

/// <summary>
/// Категорії — довідник, що майже не змінюється: список кешується дворівнево з absolute TTL
/// (L1 10 хв, Redis 1 год) і видаляється з кешу при створенні категорії, а також коли курс
/// змінює набір категорій (CourseService).
/// </summary>
public class CategoryService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache, ILogger<CategoryService> logger) : ICategoryService
{
    /// <summary>Якщо дату не вказано — курси, додані за останні 90 днів.</summary>
    private static readonly TimeSpan DefaultRecentPeriod = TimeSpan.FromDays(90);

    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync<IReadOnlyList<CategoryDto>>(CacheKeys.Categories, async token =>
        {
            var categories = await uow.Categories.GetAllAsync(token);
            var counts = (await uow.CourseCategories.CountCoursesPerCategoryAsync(token))
                .ToDictionary(count => count.CategoryId, count => count.CoursesCount);

            return categories
                .OrderBy(category => category.Name)
                .Select(category => new CategoryDto(
                    category.Id, category.Name, category.Slug, category.Description,
                    counts.GetValueOrDefault(category.Id)))
                .ToList();
        }, CachePolicies.Categories, ct);

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
        await cache.InvalidateAsync("CategoryCreated", [CacheKeys.Categories], ct);

        logger.LogInformation("Category {CategoryId} '{CategorySlug}' created", category.Id, category.Slug);
        return new CategoryDto(category.Id, category.Name, category.Slug, category.Description, CoursesCount: 0);
    }
}
