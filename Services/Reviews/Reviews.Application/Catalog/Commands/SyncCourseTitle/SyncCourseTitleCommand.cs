using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Catalog.Commands.SyncCourseTitle;

/// <summary>
/// Курс перейменовано в Каталозі — оновити денормалізований courseTitle у відгуках і обговореннях
/// (політика дублювання Лаб. 01). Надсилає обробник інтеграційної події course.renamed (Лаб. 08).
/// </summary>
public sealed record SyncCourseTitleCommand(
    Guid EventId,
    string EventType,
    string Consumer,
    long CourseId,
    string Title,
    DateTime OccurredAt) : ICommand<CourseTitleSyncResult>;

/// <param name="Outcome">applied — оновлено; duplicate — подію вже оброблено; stale — подія старша за застосовану.</param>
public sealed record CourseTitleSyncResult(string Outcome, long ReviewsUpdated, long DiscussionsUpdated)
{
    public static readonly CourseTitleSyncResult Duplicate = new("duplicate", 0, 0);
    public static readonly CourseTitleSyncResult Stale = new("stale", 0, 0);
}
