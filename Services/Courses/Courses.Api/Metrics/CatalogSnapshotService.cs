using Courses.Bll.Dtos;
using Courses.Bll.Services;

namespace Courses.Api.Metrics;

/// <summary>
/// Раз на 30 с рахує опубліковані й неопубліковані курси для gauge courses.catalog.size.
/// Запити — сторінки по одному елементу (рахується лише COUNT) і йдуть через кеш каталогу,
/// тож базу майже не навантажують. Збій лише логується.
/// </summary>
public sealed class CatalogSnapshotService(
    IServiceScopeFactory scopes,
    CatalogMetrics metrics,
    ILogger<CatalogSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var courses = scope.ServiceProvider.GetRequiredService<ICourseService>();

                var published = await courses.GetListAsync(new CourseQuery { IsPublished = true, PageSize = 1 }, stoppingToken);
                var draft = await courses.GetListAsync(new CourseQuery { IsPublished = false, PageSize = 1 }, stoppingToken);
                metrics.UpdateSnapshot(published.TotalCount, draft.TotalCount);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Catalog snapshot for metrics failed: {FailureType} {FailureMessage}",
                    exception.GetType().Name, exception.GetBaseException().Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
