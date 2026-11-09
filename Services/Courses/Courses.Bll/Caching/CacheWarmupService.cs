using System.Diagnostics;
using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Courses.Bll.Caching;

/// <summary>
/// Прогрівання кешу: одразу після старту й далі періодично завантажує те, що запитують найчастіше, —
/// довідник категорій і картки опублікованих курсів. Читання йде звичайними методами сервісів,
/// тож прогрівання проходить той самий cache-aside і заповнює і L1, і Redis.
/// Помилка прогрівання не зупиняє сервіс: кеш просто заповниться першими запитами.
/// </summary>
public sealed class CacheWarmupService(
    IServiceScopeFactory scopes,
    IOptions<CachingOptions> options,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.WarmupOnStartup)
            return;

        do
        {
            await WarmUpAsync(settings.WarmupCourses, stoppingToken);

            if (settings.WarmupInterval <= TimeSpan.Zero)
                return;

            await Task.Delay(settings.WarmupInterval, stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested);
    }

    private async Task WarmUpAsync(int coursesCount, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            var courses = scope.ServiceProvider.GetRequiredService<ICourseService>();

            var categoryList = await categories.GetAllAsync(ct);

            var page = await courses.GetListAsync(
                new CourseQuery { IsPublished = true, PageSize = coursesCount, SortBy = "rating", SortDir = "desc" }, ct);
            foreach (var course in page.Items)
                await courses.GetByIdAsync(course.Id, ct);

            logger.LogInformation("Cache warm-up done: {CategoriesCount} categories, {CoursesCount} course cards in {ElapsedMs} ms",
                categoryList.Count, page.Items.Count, Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Cache warm-up failed; the cache will be filled by requests");
        }
    }
}
