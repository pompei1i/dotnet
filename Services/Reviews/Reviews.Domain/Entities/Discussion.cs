using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Тема обговорення курсу — документ колекції discussions з Лаб. 01: питання студента
/// або оголошення викладача. Повідомлення лежать в окремій колекції messages,
/// а в темі зберігаються лише їх кількість і знімок останнього (lastMessage).
/// </summary>
public class Discussion : BaseEntity
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 300;
    public const int MaxTags = 10;
    public const int MaxTagLength = 50;

    private List<string> _tags = [];

    public long CourseId { get; private set; }

    public string CourseTitle { get; private set; } = "";

    public string Title { get; private set; } = "";

    public DiscussionType Type { get; private set; }

    public Participant Author { get; private set; } = null!;

    public DiscussionStatus Status { get; private set; }

    public int MessagesCount { get; private set; }

    public MessagePreview? LastMessage { get; private set; }

    /// <summary>Прийнята відповідь (посилання на messages) — лише у вирішених питань.</summary>
    public ObjectId? AcceptedAnswerId { get; private set; }

    /// <summary>Відгук, з якого виросла тема.</summary>
    public ObjectId? SourceReviewId { get; private set; }

    /// <summary>Є лише в оголошень; у питань поле в документі відсутнє.</summary>
    public bool? IsPinned { get; private set; }

    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    private Discussion() { }   // для десеріалізації

    public Discussion(
        long courseId,
        string courseTitle,
        string title,
        DiscussionType type,
        Participant author,
        IEnumerable<string>? tags = null,
        ObjectId? sourceReviewId = null)
    {
        Author = author ?? throw new DomainException("Author is required");
        if (type == DiscussionType.Announcement && !author.IsInstructor)
            throw new DomainException("Only an instructor can publish an announcement");

        CourseId = Guard.Id(courseId, "CourseId");
        CourseTitle = Guard.Text(courseTitle, "CourseTitle", Review.MaxCourseTitleLength);
        Title = Guard.Text(title, "Title", MaxTitleLength);
        if (Title.Length < MinTitleLength)
            throw new DomainException($"Title must be at least {MinTitleLength} characters");

        Type = type;
        Status = DiscussionStatus.Open;
        _tags = Guard.Items(tags, "Tags", MaxTags, MaxTagLength).ConvertAll(tag => tag.ToLowerInvariant());
        SourceReviewId = sourceReviewId;
        IsPinned = type == DiscussionType.Announcement ? false : null;

        // updatedAt — час останньої активності; список тем сортується за ним, тож нова тема має його одразу
        UpdatedAt = CreatedAt;
    }

    /// <summary>Переводить тему в новий стан, перевіряючи допустимість переходу.</summary>
    public void ChangeStatus(DiscussionStatus status)
    {
        switch (status)
        {
            case DiscussionStatus.Resolved: Resolve(); break;
            case DiscussionStatus.Closed: Close(); break;
            case DiscussionStatus.Open: Reopen(); break;
            default: throw new DomainException($"Unknown status {status}");
        }
    }

    /// <summary>Вирішити можна лише відкрите питання; оголошення не «вирішуються».</summary>
    public void Resolve()
    {
        if (Type != DiscussionType.Question)
            throw new DomainException("Only a question can be resolved");
        if (Status != DiscussionStatus.Open)
            throw new DomainException($"Only an open question can be resolved; the discussion is {Status.ToString().ToLowerInvariant()}");
        Status = DiscussionStatus.Resolved;
        Touch();
    }

    public void Close()
    {
        if (Status == DiscussionStatus.Closed)
            throw new DomainException("Discussion is already closed");
        Status = DiscussionStatus.Closed;
        Touch();
    }

    /// <summary>Повторне відкриття знімає прийняту відповідь: тема знову чекає на рішення.</summary>
    public void Reopen()
    {
        if (Status == DiscussionStatus.Open)
            throw new DomainException("Discussion is already open");
        Status = DiscussionStatus.Open;
        AcceptedAnswerId = null;
        Touch();
    }

    public void Pin(bool pinned = true)
    {
        if (Type != DiscussionType.Announcement)
            throw new DomainException("Only an announcement can be pinned");
        IsPinned = pinned;
        Touch();
    }
}
