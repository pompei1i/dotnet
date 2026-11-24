using FluentValidation;
using MediatR;
using Platform.EventBus;
using Reviews.Application.Catalog.Commands.SyncCourseTitle;
using Reviews.WebApi.IntegrationEvents.Events;

namespace Reviews.WebApi.IntegrationEvents.Handlers;

/// <summary>
/// Обробник course.renamed (Лаб. 08): перетворює інтеграційну подію на команду Application-шару,
/// як контролер — HTTP-запит. Команда проходить увесь pipeline MediatR (трасування, логування, валідація,
/// метрики), а ідемпотентність і оновлення courseTitle — у SyncCourseTitleCommandHandler.
/// Помилка валідації — постійний збій: повідомлення одразу йде в DLQ, без повторів.
/// </summary>
public sealed class CourseRenamedHandler(ISender sender) : IIntegrationEventHandler<CourseRenamedIntegrationEvent>
{
    public const string Consumer = "reviews.catalog-sync";

    public async Task HandleAsync(CourseRenamedIntegrationEvent @event, CancellationToken ct)
    {
        try
        {
            await sender.Send(new SyncCourseTitleCommand(
                @event.EventId,
                nameof(CourseRenamedIntegrationEvent),
                Consumer,
                @event.CourseId,
                @event.NewTitle,
                @event.OccurredAt), ct);
        }
        catch (ValidationException exception)
        {
            throw new PermanentEventException($"Invalid {nameof(CourseRenamedIntegrationEvent)} {@event.EventId}: {exception.Message}", exception);
        }
    }
}
