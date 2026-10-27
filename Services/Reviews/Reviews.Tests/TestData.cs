using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Tests;

internal static class TestData
{
    static TestData() => MongoMappings.Register();

    public static Review Review(long courseId = 1, long studentId = 7, int rating = 5, string? text = "Корисний курс")
        => new(courseId, "C# для початківців", new Author(studentId, "Олена Коваль"), new Rating(rating), text,
            pros: ["Практика"], cons: []);

    public static Participant Instructor => new(1, ParticipantRole.Instructor, "Андрій Мельник");

    public static Participant Student => new(3, ParticipantRole.Student, "Софія Лисенко");

    public static Discussion Question()
        => new(1, "C# для початківців", "Чи достатньо цього курсу?", DiscussionType.Question, Student, ["CSharp", "csharp", "plan"]);

    /// <summary>Мапер із профілями Application (сутність -> DTO) і WebApi (контракт -> команда).</summary>
    public static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.AddMaps(
                typeof(Application.DependencyInjection).Assembly,
                typeof(WebApi.DependencyInjection).Assembly),
            NullLoggerFactory.Instance).CreateMapper();

    /// <summary>Гарантує, що BSON-мапінг зареєстровано до першої серіалізації в тесті.</summary>
    public static void EnsureMappings() => MongoMappings.Register();
}
