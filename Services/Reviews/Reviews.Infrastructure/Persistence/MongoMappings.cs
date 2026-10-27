using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence.Serializers;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// BSON-мапінг доменних класів без жодного атрибута в самому домені. Документи мають ту саму
/// структуру, що в Лаб. 01: імена полів у camelCase, вкладені author і comments, порожні
/// необов'язкові поля не записуються (гнучка схема). Реєструється один раз під час старту,
/// до першого звернення до бази; повторний виклик нічого не робить.
/// </summary>
public static class MongoMappings
{
    private static readonly Lock Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered)
                return;

            ExcludeEntitiesFromImmutableTypeConvention();

            // Конвенції діють лише на власні типи сервісу (сутності, Value Objects, DTO)
            ConventionRegistry.Register(
                "reviews-service",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true),   // поля, яких немає в класі, не ламають читання
                    new IgnoreIfNullConvention(true)            // null не записується: text, videoUrl, discussionId...
                },
                type => type.Namespace?.StartsWith("Reviews.", StringComparison.Ordinal) == true);

            // Value Objects — до сутностей: AutoMap сутності бере вже зареєстровані серіалізатори
            RegisterValueObjects();
            RegisterEntities();

            _registered = true;
        }
    }

    private static void RegisterEntities()
    {
        BsonClassMap.TryRegisterClassMap<BaseEntity>(cm =>
        {
            cm.AutoMap();
            cm.MapIdProperty(e => e.Id);   // Id -> _id
        });

        BsonClassMap.TryRegisterClassMap<Review>(cm =>
        {
            cm.AutoMap();

            // Публічні колекції — лише read-only обгортки над приватними полями; у документ іде поле
            cm.UnmapProperty(r => r.Comments);
            cm.UnmapProperty(r => r.Photos);
            cm.UnmapProperty(r => r.Pros);
            cm.UnmapProperty(r => r.Cons);

            // Приватні поля вбудованих масивів; порожній масив не записується —
            // відгук «лише оцінка» не має полів comments, photos, pros, cons
            MapList(cm, "_comments", "comments", r => r.Comments.Count);
            MapList(cm, "_photos", "photos", r => r.Photos.Count);
            MapList(cm, "_pros", "pros", r => r.Pros.Count);
            MapList(cm, "_cons", "cons", r => r.Cons.Count);
        });

        BsonClassMap.TryRegisterClassMap<Comment>(cm => cm.AutoMap());

        BsonClassMap.TryRegisterClassMap<Discussion>(cm =>
        {
            cm.AutoMap();
            cm.UnmapProperty(d => d.Tags);
            cm.MapProperty(d => d.Type).SetSerializer(new LowerCaseEnumSerializer<DiscussionType>());
            cm.MapProperty(d => d.Status).SetSerializer(new LowerCaseEnumSerializer<DiscussionStatus>());
            MapList(cm, "_tags", "tags", d => d.Tags.Count);
        });
    }

    /// <summary>
    /// Value Objects незмінні: властивості без сетерів, тож драйвер створює їх через конструктор
    /// (MapCreator) — і валідація конструктора спрацьовує й під час читання з бази.
    /// </summary>
    private static void RegisterValueObjects()
    {
        BsonClassMap.TryRegisterClassMap<Author>(cm =>
        {
            cm.MapProperty(a => a.StudentId).SetElementName("studentId");
            cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
            cm.MapCreator(a => new Author(a.StudentId, a.DisplayName));
        });

        BsonClassMap.TryRegisterClassMap<Participant>(cm =>
        {
            cm.MapProperty(p => p.UserId).SetElementName("userId");
            cm.MapProperty(p => p.Role).SetElementName("role")
                .SetSerializer(new LowerCaseEnumSerializer<ParticipantRole>());
            cm.MapProperty(p => p.DisplayName).SetElementName("displayName");
            cm.MapCreator(p => new Participant(p.UserId, p.Role, p.DisplayName));
        });

        BsonClassMap.TryRegisterClassMap<MessagePreview>(cm =>
        {
            cm.MapProperty(m => m.MessageId).SetElementName("messageId");
            cm.MapProperty(m => m.AuthorName).SetElementName("authorName");
            cm.MapProperty(m => m.TextPreview).SetElementName("textPreview");
            cm.MapProperty(m => m.CreatedAt).SetElementName("createdAt");
            cm.MapCreator(m => new MessagePreview(m.MessageId, m.AuthorName, m.TextPreview, m.CreatedAt));
        });

        // Rating — власний серіалізатор: у документі звичайне число rating: 5, а не { value: 5 }
        BsonSerializer.TryRegisterSerializer(new RatingSerializer());
    }

    /// <summary>
    /// Стандартна ImmutableTypeClassMapConvention вважає клас без публічних сетерів і без публічного
    /// конструктора за замовчуванням «незмінним» і робить творцем його публічний конструктор.
    /// Для сутностей це хибно: публічний конструктор містить бізнес-валідацію для нових об'єктів,
    /// а читання з бази має йти через приватний конструктор без параметрів. Тож для сутностей
    /// конвенцію вимкнено; для DTO-record-ів вона лишається — ними драйвер створює проєкції.
    /// </summary>
    private static void ExcludeEntitiesFromImmutableTypeConvention()
    {
        const string defaultsName = "__defaults__";
        var defaults = new ConventionPack();
        defaults.AddRange(DefaultConventionPack.Instance.Conventions.Where(c => c is not ImmutableTypeClassMapConvention));

        ConventionRegistry.Remove(defaultsName);
        ConventionRegistry.Register(defaultsName, defaults, _ => true);
        ConventionRegistry.Register(
            "immutable-types-except-entities",
            new ConventionPack { new ImmutableTypeClassMapConvention() },
            type => !typeof(BaseEntity).IsAssignableFrom(type) && type != typeof(Comment));
    }

    private static void MapList<T>(BsonClassMap<T> cm, string fieldName, string elementName, Func<T, int> count)
        => cm.MapField(fieldName)
            .SetElementName(elementName)
            .SetShouldSerializeMethod(entity => count((T)entity) > 0);
}
