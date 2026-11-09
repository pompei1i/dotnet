using AutoMapper;
using Courses.Bll.Caching;
using Courses.Bll.Diagnostics;
using Courses.Bll.Dtos;
using Courses.Bll.Specifications;
using Courses.Dal;
using Courses.Domain.Entities;
using Courses.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Courses.Bll.Services;

/// <summary>
/// Бізнес-операції з курсами. Сервіс не знає про EF Core: працює через Unit of Work,
/// а зберігає зміни одним SaveChangesAsync наприкінці операції.
/// Правила самої сутності (ціна, публікація) перевіряє Course; сервіс перевіряє те,
/// для чого потрібні інші дані з бази, — унікальність Slug, існування викладача й категорій.
///
/// Лаб. 06: читання — cache-aside через дворівневий кеш (L1 пам'ять -> L2 Redis -> база), кожен запис
/// одразу після SaveChangesAsync видаляє пов'язані ключі: картку курсу, сторінки списку (через ключ-версію)
/// і довідник категорій, якщо змінилася кількість курсів у категоріях. REST-контролер і gRPC-сервіс
/// викликають ці самі методи, тож кеш працює для обох протоколів без дублювання.
/// </summary>
public class CourseService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache, ILogger<CourseService> logger) : ICourseService
{
    public async Task<PagedResult<CourseDto>> GetListAsync(CourseQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, CourseQuery.MaxPageSize);

        var version = await cache.GetVersionAsync(CacheKeys.ListVersion, ct);
        var key = CacheKeys.CourseListPage(version, query, page, pageSize);

        return await cache.GetOrCreateAsync(key, async token =>
        {
            var specification = new CoursesFilterSpec(query, page, pageSize);

            // Обидва запити рахує база: COUNT за тими самими критеріями і одна сторінка через OFFSET ... FETCH
            var totalCount = await uow.Courses.CountAsync(specification, token);
            var courses = await uow.Courses.ListAsync(specification, token);

            return new PagedResult<CourseDto>(mapper.Map<IReadOnlyList<CourseDto>>(courses), totalCount, page, pageSize);
        }, CachePolicies.CourseListPage, ct);
    }

    public Task<CourseDetailsDto> GetByIdAsync(long id, CancellationToken ct = default)
        => cache.GetOrCreateAsync(CacheKeys.Course(id), async token =>
        {
            // Неіснуючий курс -> виняток із фабрики, тож у кеш нічого не потрапляє
            var course = await uow.Courses.GetWithDetailsAsync(id, token)
                         ?? throw new EntityNotFoundException(nameof(Course), id);

            return mapper.Map<CourseDetailsDto>(course);
        }, CachePolicies.CourseCard, ct);

    /// <summary>Курси за списком Id (пакетне читання для gRPC) — кожна картка з кешу; відсутні пропускаються.</summary>
    public async Task<IReadOnlyList<CourseDetailsDto>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        var result = new List<CourseDetailsDto>(ids.Count);
        foreach (var id in ids.Distinct())
        {
            try
            {
                result.Add(await GetByIdAsync(id, ct));
            }
            catch (EntityNotFoundException)
            {
                // відсутній курс не скасовує решту: клієнт порівнює Id відповіді із запитаними
            }
        }
        return result;
    }

    public async Task<CourseDetailsDto> CreateAsync(CreateCourseDto input, CancellationToken ct = default)
    {
        // Власний span бізнес-операції: усередині нього — SQL-spans перевірок і INSERT від EF Core
        using var activity = CoursesTelemetry.Source.StartActivity("CreateCourse");
        activity?.SetTag("instructor.id", input.InstructorId);
        activity?.SetTag("course.slug", input.Slug);

        var instructor = await uow.Instructors.GetByIdAsync(input.InstructorId, ct)
                         ?? throw new EntityNotFoundException(nameof(Instructor), input.InstructorId);

        if (await uow.Courses.ExistsBySlugAsync(input.Slug, ct))
            throw new ConflictException($"Course with slug '{input.Slug}' already exists");

        var categoryIds = input.CategoryIds.Distinct().ToList();
        var categories = await uow.Categories.GetByIdsAsync(categoryIds, ct);
        var missing = categoryIds.Except(categories.Select(category => category.Id)).ToList();
        if (missing.Count > 0)
            throw new EntityNotFoundException(nameof(Category), string.Join(", ", missing));

        var course = new Course(
            instructor, input.Title, input.Slug, input.Price,
            Enum.Parse<CourseLevel>(input.Level, ignoreCase: true), input.Language);

        course.SetDetail(input.Description, input.Requirements, input.DurationHours, input.HasCertificate);

        var primaryCategoryId = input.PrimaryCategoryId ?? categoryIds[0];
        var addedAt = DateTime.UtcNow;
        foreach (var category in categories.OrderBy(category => category.Id == primaryCategoryId ? 0 : 1))
            course.AssignCategory(category, isPrimary: category.Id == primaryCategoryId, addedAt);

        await uow.Courses.AddAsync(course, ct);
        await uow.SaveChangesAsync(ct);   // Courses, CourseDetails і CourseCategories — однією транзакцією

        activity?.SetTag("course.id", course.Id);

        // Новий курс з'являється в списках і змінює кількість курсів у категоріях
        await InvalidateAsync("CourseCreated", course.Id, lists: true, categories: true, ct);

        logger.LogInformation("Course {CourseId} '{CourseSlug}' created for instructor {InstructorId} with price {Price}",
            course.Id, course.Slug, instructor.Id, course.Price);

        return mapper.Map<CourseDetailsDto>(course);
    }

    public async Task ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct = default)
    {
        using var activity = CoursesTelemetry.Source.StartActivity("ChangeCoursePrice");
        activity?.SetTag("course.id", id);
        activity?.SetTag("course.new_price", input.Price);

        var course = await uow.Courses.GetByIdAsync(id, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), id);

        var oldPrice = course.Price;
        course.ChangePrice(input.Price);
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("CoursePriceChanged", id, lists: true, categories: false, ct);

        logger.LogInformation("Course {CourseId} price changed from {OldPrice} to {NewPrice}", id, oldPrice, course.Price);
    }

    public async Task PublishAsync(long id, CancellationToken ct = default)
    {
        using var activity = CoursesTelemetry.Source.StartActivity("PublishCourse");
        activity?.SetTag("course.id", id);

        // Публікація перевіряє опис і уроки, тому курс читається разом з ними
        var course = await uow.Courses.GetWithDetailsAsync(id, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), id);

        course.Publish(DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("CoursePublished", id, lists: true, categories: false, ct);

        logger.LogInformation("Course {CourseId} published, status {Status}", id, "Published");
    }

    public async Task UnpublishAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), id);

        course.Unpublish();
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("CourseUnpublished", id, lists: true, categories: false, ct);

        logger.LogInformation("Course {CourseId} unpublished", id);
    }

    public async Task<LessonDto> AddLessonAsync(long courseId, CreateLessonDto input, CancellationToken ct = default)
    {
        // Номер нового уроку рахується з наявних, тому курс читається разом з уроками
        var course = await uow.Courses.GetWithDetailsAsync(courseId, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), courseId);

        var lesson = course.AddLesson(input.Title, input.DurationMinutes, input.IsFreePreview);
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("LessonAdded", courseId, lists: false, categories: false, ct);   // уроки є лише в картці

        logger.LogInformation("Lesson {LessonId} #{OrderIndex} added to course {CourseId}", lesson.Id, lesson.OrderIndex, courseId);
        return mapper.Map<LessonDto>(lesson);
    }

    public async Task AssignCategoryAsync(long courseId, AssignCategoryDto input, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetWithDetailsAsync(courseId, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), courseId);
        var category = await uow.Categories.GetByIdAsync(input.CategoryId, ct)
                       ?? throw new EntityNotFoundException(nameof(Category), input.CategoryId);

        course.AssignCategory(category, input.IsPrimary, DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("CourseCategoryAssigned", courseId, lists: true, categories: true, ct);

        logger.LogInformation("Course {CourseId} added to category {CategoryId} (primary: {IsPrimary})",
            courseId, input.CategoryId, input.IsPrimary);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
                     ?? throw new EntityNotFoundException(nameof(Course), id);

        course.Delete();   // м'яке видалення; опублікований курс сутність не дасть видалити
        await uow.SaveChangesAsync(ct);
        await InvalidateAsync("CourseDeleted", id, lists: true, categories: true, ct);

        logger.LogInformation("Course {CourseId} deleted", id);
    }

    /// <summary>
    /// Інвалідація одразу після SaveChangesAsync і до відповіді клієнту: картка курсу, за потреби —
    /// усі сторінки списку (нова версія) і довідник категорій (кількість курсів у категорії).
    /// </summary>
    private async Task InvalidateAsync(string reason, long courseId, bool lists, bool categories, CancellationToken ct)
    {
        var keys = categories ? new[] { CacheKeys.Course(courseId), CacheKeys.Categories } : [CacheKeys.Course(courseId)];
        await cache.InvalidateAsync(reason, keys, ct);

        if (lists)
            await cache.BumpVersionAsync(CacheKeys.ListVersion, reason, ct);
    }
}
