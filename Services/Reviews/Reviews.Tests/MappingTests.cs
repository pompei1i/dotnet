using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Reviews.Application.Discussions.Dtos;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Dtos;
using Reviews.WebApi.Contracts;

namespace Reviews.Tests;

public class MappingTests
{
    [Fact]
    public void AutoMapper_configuration_is_valid()
        => new MapperConfiguration(cfg => cfg.AddMaps(
                typeof(Application.DependencyInjection).Assembly,
                typeof(WebApi.DependencyInjection).Assembly),
            NullLoggerFactory.Instance).AssertConfigurationIsValid();

    [Fact]
    public void Review_maps_ObjectId_value_objects_and_embedded_array()
    {
        var review = TestData.Review();
        review.AddComment(TestData.Instructor, "Дякую");

        var dto = TestData.CreateMapper().Map<ReviewDto>(review);

        Assert.Equal(review.Id.ToString(), dto.Id);                   // ObjectId -> рядок
        Assert.Equal(5, dto.Rating);                                   // Value Object Rating -> число
        Assert.Equal(new AuthorDto(7, "Олена Коваль"), dto.Author);    // Value Object Author -> DTO
        var comment = Assert.Single(dto.Comments);                     // вбудований масив
        Assert.Equal(new ParticipantDto(1, "instructor", "Андрій Мельник"), comment.Author);
        Assert.Null(dto.DiscussionId);                                 // відсутній ObjectId? -> null
    }

    [Fact]
    public void Discussion_maps_enums_to_lowercase_strings()
    {
        var dto = TestData.CreateMapper().Map<DiscussionDto>(TestData.Question());

        Assert.Equal("question", dto.Type);
        Assert.Equal("open", dto.Status);
        Assert.Equal("student", dto.Author.Role);
        Assert.Null(dto.LastMessage);
    }

    [Fact]
    public void Request_maps_to_command_and_route_values_are_added_with()
    {
        var command = TestData.CreateMapper().Map<UpdateReviewCommand>(new UpdateReviewRequest(4, "Текст", ["a"], null))
            with { Id = "670000000000000000000101", ExpectedVersion = 2 };

        Assert.Equal("670000000000000000000101", command.Id);
        Assert.Equal(2, command.ExpectedVersion);
        Assert.Equal(4, command.Rating);
        Assert.Equal(["a"], command.Pros);
        Assert.Empty(command.Cons!);                    // AutoMapper: відсутній список -> порожній
    }

    [Fact]
    public void Review_without_comments_or_update_has_no_such_fields_in_the_document()
    {
        TestData.EnsureMappings();
        var names = TestData.Review().ToBsonDocument().Names.ToList();

        Assert.DoesNotContain("updatedAt", names);
        Assert.DoesNotContain("comments", names);
    }
}
