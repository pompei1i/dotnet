using NSubstitute;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Discussions.Queries.GetDiscussionsList;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

public class ValidatorTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    private static CreateReviewCommand ValidCreate(int rating = 5)
        => new(1, "C# для початківців", 7, "Олена Коваль", rating, "Текст", ["Практика"], null);

    [Fact]
    public async Task Rating_10_is_rejected()
    {
        var result = await new CreateReviewCommandValidator(_reviews).ValidateAsync(ValidCreate(rating: 10));

        Assert.Contains(result.Errors, e => e.PropertyName == "Rating");
    }

    [Fact]
    public async Task All_errors_are_collected_together()
    {
        var command = ValidCreate(rating: 0) with { CourseId = 0, CourseTitle = "", AuthorName = "", Pros = ["", "ok"] };

        var result = await new CreateReviewCommandValidator(_reviews).ValidateAsync(command);

        var properties = result.Errors.Select(e => e.PropertyName).ToHashSet();
        Assert.Superset(new HashSet<string> { "CourseId", "CourseTitle", "AuthorName", "Rating", "Pros" }, properties);
    }

    [Fact]
    public async Task Second_review_of_the_same_course_is_rejected_by_the_async_rule()
    {
        _reviews.ExistsAsync(1, 7, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new CreateReviewCommandValidator(_reviews).ValidateAsync(ValidCreate());

        var error = Assert.Single(result.Errors);
        Assert.Equal("StudentId", error.PropertyName);
        Assert.Equal("Student 7 has already reviewed course 1", error.ErrorMessage);
    }

    [Fact]
    public async Task Valid_command_passes_and_checks_the_database_once()
    {
        var result = await new CreateReviewCommandValidator(_reviews).ValidateAsync(ValidCreate());

        Assert.True(result.IsValid);
        await _reviews.Received(1).ExistsAsync(1, 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("670000000000000000000101", true)]
    [InlineData("67000000000000000000010", false)]     // 23 символи
    [InlineData("67000000000000000000010g", false)]    // не шістнадцятковий символ
    [InlineData("", false)]
    public void ObjectId_format_is_checked(string id, bool valid)
        => Assert.Equal(valid, new GetReviewByIdQueryValidator().Validate(new GetReviewByIdQuery(id)).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]                             // некоректний If-Match
    public void Expected_version_from_If_Match_is_checked(int? version, bool valid)
    {
        var command = new UpdateReviewCommand("670000000000000000000101", 4, null, null, null, version);
        Assert.Equal(valid, new UpdateReviewCommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData("student", true)]
    [InlineData("Instructor", true)]
    [InlineData("admin", false)]
    [InlineData("1", false)]                           // числове значення перерахування не приймається
    public void Role_must_be_a_known_value(string role, bool valid)
    {
        var command = new AddCommentCommand("670000000000000000000101", 1, role, "Ім'я", "Текст");
        Assert.Equal(valid, new AddCommentCommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData("createdAt", "desc", 1, 20, true)]
    [InlineData("rating", "ASC", 2, 100, true)]
    [InlineData("password", "desc", 1, 20, false)]
    [InlineData("0", "desc", 1, 20, false)]
    [InlineData("createdAt", "up", 1, 20, false)]
    [InlineData("createdAt", "desc", 0, 20, false)]
    [InlineData("createdAt", "desc", 1, 101, false)]
    public void List_query_parameters_are_checked(string sortBy, string sortDir, int page, int pageSize, bool valid)
    {
        var query = new GetReviewsListQuery(SortBy: sortBy, SortDir: sortDir, Page: page, PageSize: pageSize);
        Assert.Equal(valid, new GetReviewsListQueryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Discussion_status_filter_must_be_a_known_value()
    {
        Assert.True(new GetDiscussionsListQueryValidator().Validate(new GetDiscussionsListQuery(Status: "open")).IsValid);
        Assert.False(new GetDiscussionsListQueryValidator().Validate(new GetDiscussionsListQuery(Status: "archived")).IsValid);
    }

    [Fact]
    public async Task Source_review_must_exist_and_belong_to_the_same_course()
    {
        var review = TestData.Review(courseId: 2);
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var validator = new CreateDiscussionCommandValidator(_reviews);

        CreateDiscussionCommand Command(long courseId, string? sourceReviewId) => new(
            courseId, "C#", "Питання", "question", 3, "student", "Софія", null, sourceReviewId);

        Assert.True((await validator.ValidateAsync(Command(2, review.Id.ToString()))).IsValid);
        Assert.False((await validator.ValidateAsync(Command(1, review.Id.ToString()))).IsValid);
        Assert.False((await validator.ValidateAsync(Command(2, "670000000000000000000999"))).IsValid);

        // Некоректний формат зупиняє правило ще до запиту до бази (Cascade.Stop)
        var untouched = Substitute.For<IReviewRepository>();
        Assert.False((await new CreateDiscussionCommandValidator(untouched).ValidateAsync(Command(2, "bad-id"))).IsValid);
        await untouched.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }
}
