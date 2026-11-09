namespace Platform.Aggregator.Models;

// Композитні DTO агрегатора. Їхня структура відповідає бізнес-сценаріям (картка замовлення,
// огляд курсу), а не копіює відповіді окремих сервісів: дані каталогу й відгуків вкладено
// в позицію замовлення, якої вони стосуються. JSON — camelCase; null-поля (дані недоступного
// сервісу) не серіалізуються, а причина їхньої відсутності — у Warnings.

/// <summary>
/// Повна картка замовлення: замовлення (enrollments-api) + актуальні дані курсів (courses-api)
/// + рейтинг і відгуки про ці курси (reviews-api).
/// </summary>
public sealed record OrderCardDto(
    long OrderId,
    long StudentId,
    string Status,
    DateTime OrderDate,
    decimal TotalAmount,
    string? PaymentMethod,
    IReadOnlyList<OrderCardItemDto> Items,
    IReadOnlyList<SourceStatusDto> Sources,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Позиція замовлення. Title, UnitPrice і Discount — знімок на момент оформлення (enrollments-api);
/// Course — актуальний стан курсу в каталозі; PriceChanged — чи змінилася ціна відтоді.
/// </summary>
public sealed record OrderCardItemDto(
    long CourseId,
    string Title,
    decimal UnitPrice,
    decimal Discount,
    decimal LineTotal,
    bool? PriceChanged,
    CourseInfoDto? Course,
    CourseFeedbackDto? Feedback);

/// <summary>Огляд курсу для викладача: каталог + відгуки + продажі — три сервіси паралельно.</summary>
public sealed record CourseOverviewDto(
    long CourseId,
    CourseInfoDto? Course,
    CourseFeedbackDto? Feedback,
    CourseSalesDto? Sales,
    IReadOnlyList<SourceStatusDto> Sources,
    IReadOnlyList<string> Warnings);

public sealed record CourseInfoDto(
    string Title,
    string Slug,
    string Level,
    string Language,
    bool IsPublished,
    decimal CurrentPrice,
    string InstructorName,
    decimal? DurationHours,
    bool? HasCertificate,
    int? LessonsCount);

public sealed record CourseFeedbackDto(
    int ReviewsCount,
    double? AverageRating,
    IReadOnlyDictionary<string, int>? Distribution,
    IReadOnlyList<ReviewSnippetDto> LatestReviews);

public sealed record ReviewSnippetDto(
    string Id,
    string AuthorName,
    int Rating,
    string? Text,
    bool IsVerifiedPurchase,
    DateTime CreatedAt);

public sealed record CourseSalesDto(int OrdersCount, decimal Revenue);

/// <summary>Звідки прийшли дані: сервіс, результат ("ok" або "unavailable") і час відповіді.</summary>
public sealed record SourceStatusDto(string Service, string Status, double ElapsedMs);
