using Enrollments.Api.IntegrationEvents.Events;
using Platform.EventBus;

namespace Enrollments.Api.IntegrationEvents.Handlers;

// Окремий обробник для кожного типу події; створюється в DI-scope повідомлення. Кожен лише дістає з події
// знімок курсу й передає його CatalogCopyUpdater (ідемпотентність і транзакція — там).

/// <summary>course.price_changed — нова ціна в локальному довіднику; ціни в оформлених замовленнях не змінюються.</summary>
public sealed class CoursePriceChangedHandler(CatalogCopyUpdater updater) : IIntegrationEventHandler<CoursePriceChangedIntegrationEvent>
{
    public Task HandleAsync(CoursePriceChangedIntegrationEvent e, CancellationToken ct)
        => updater.ApplyAsync(e, e.CourseId, e.Title, e.NewPrice, e.IsPublished, ct);
}

/// <summary>course.renamed — нова назва в локальному довіднику (у позиціях замовлень лишається назва на момент покупки).</summary>
public sealed class CourseRenamedHandler(CatalogCopyUpdater updater) : IIntegrationEventHandler<CourseRenamedIntegrationEvent>
{
    public Task HandleAsync(CourseRenamedIntegrationEvent e, CancellationToken ct)
        => updater.ApplyAsync(e, e.CourseId, e.NewTitle, e.Price, e.IsPublished, ct);
}

/// <summary>course.published / course.unpublished — IsAvailable: знятий з публікації курс більше не купують.</summary>
public sealed class CoursePublicationChangedHandler(CatalogCopyUpdater updater) : IIntegrationEventHandler<CoursePublicationChangedIntegrationEvent>
{
    public Task HandleAsync(CoursePublicationChangedIntegrationEvent e, CancellationToken ct)
        => updater.ApplyAsync(e, e.CourseId, e.Title, e.Price, e.IsPublished, ct);
}
