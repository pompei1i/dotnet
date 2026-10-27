using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Відгук студента про курс — документ колекції reviews з Лаб. 01.
/// Автор і коментарі вбудовані; text, photos, videoUrl, pros, cons, comments і discussionId
/// необов'язкові, тож у колекції лежать документи з різними наборами полів.
/// </summary>
public class Review : BaseEntity
{
    public const int MaxCourseTitleLength = 200;
    public const int MaxTextLength = 4000;
    public const int MaxComments = 50;
    public const int MaxCommentLength = 1000;
    public const int MaxHighlights = 10;
    public const int MaxHighlightLength = 200;

    // Вбудовані масиви. Поля не readonly: драйвер MongoDB заповнює їх під час читання
    private List<Comment> _comments = [];
    private List<string> _photos = [];
    private List<string> _pros = [];
    private List<string> _cons = [];

    /// <summary>Посилання на курс у CoursesDb (без FK).</summary>
    public long CourseId { get; private set; }

    /// <summary>Назва курсу, денормалізована з CoursesDb для читання без звернення до іншого сервісу.</summary>
    public string CourseTitle { get; private set; } = "";

    public Author Author { get; private set; } = null!;

    public Rating Rating { get; private set; } = null!;

    public string? Text { get; private set; }

    /// <summary>Факт із EnrollmentsDb (доступ до курсу надано); клієнт його не задає.</summary>
    public bool IsVerifiedPurchase { get; private set; }

    public string? VideoUrl { get; private set; }

    /// <summary>Посилання на тему в discussions, якщо розмова переросла коментарі.</summary>
    public ObjectId? DiscussionId { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();

    public IReadOnlyList<string> Photos => _photos.AsReadOnly();

    public IReadOnlyList<string> Pros => _pros.AsReadOnly();

    public IReadOnlyList<string> Cons => _cons.AsReadOnly();

    private Review() { }   // для десеріалізації

    public Review(
        long courseId,
        string courseTitle,
        Author author,
        Rating rating,
        string? text,
        IEnumerable<string>? pros = null,
        IEnumerable<string>? cons = null,
        bool isVerifiedPurchase = false)
    {
        CourseId = Guard.Id(courseId, "CourseId");
        CourseTitle = Guard.Text(courseTitle, "CourseTitle", MaxCourseTitleLength);
        Author = author ?? throw new DomainException("Author is required");
        Rating = rating ?? throw new DomainException("Rating is required");
        Text = Guard.OptionalText(text, "Text", MaxTextLength);
        _pros = Guard.Items(pros, "Pros", MaxHighlights, MaxHighlightLength);
        _cons = Guard.Items(cons, "Cons", MaxHighlights, MaxHighlightLength);
        IsVerifiedPurchase = isVerifiedPurchase;
    }

    /// <summary>Автор змінює оцінку, текст і переліки переваг та недоліків.</summary>
    public void Edit(Rating rating, string? text, IEnumerable<string>? pros, IEnumerable<string>? cons)
    {
        EnsureNotDeleted();
        Rating = rating ?? throw new DomainException("Rating is required");
        Text = Guard.OptionalText(text, "Text", MaxTextLength);
        _pros = Guard.Items(pros, "Pros", MaxHighlights, MaxHighlightLength);
        _cons = Guard.Items(cons, "Cons", MaxHighlights, MaxHighlightLength);
        Touch();
    }

    /// <summary>
    /// Додає коментар у вбудований масив. Масив обмежено 50 елементами (як у валідаторі Лаб. 01):
    /// довша розмова продовжується в окремій темі discussions.
    /// </summary>
    public Comment AddComment(Participant author, string text)
    {
        EnsureNotDeleted();
        if (_comments.Count >= MaxComments)
            throw new DomainException($"A review can have at most {MaxComments} comments; continue in a discussion");

        var comment = new Comment(
            author ?? throw new DomainException("Comment author is required"),
            Guard.Text(text, "Comment text", MaxCommentLength),
            DateTime.UtcNow);
        _comments.Add(comment);
        Touch();
        return comment;
    }

    /// <summary>М'яке видалення: документ лишається (isDeleted = true), але зникає з читання.</summary>
    public void Delete()
    {
        EnsureNotDeleted();
        IsDeleted = true;
        Touch();
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Review has been deleted");
    }
}
