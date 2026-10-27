using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using NSubstitute;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

/// <summary>Обробники команд працюють лише через доменні інтерфейси репозиторіїв.</summary>
public class HandlerTests
{
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();
    private readonly IDiscussionRepository _discussions = Substitute.For<IDiscussionRepository>();

    [Fact]
    public async Task Create_adds_review_and_returns_dto_with_string_id()
    {
        var handler = new CreateReviewCommandHandler(_reviews, TestData.CreateMapper(), NullLogger<CreateReviewCommandHandler>.Instance);

        var dto = await handler.Handle(new CreateReviewCommand(3, "EF Core", 9, "Дмитро", 4, null, null, ["Мало тестів"]), default);

        Assert.Matches("^[0-9a-f]{24}$", dto.Id);
        Assert.Equal(1, dto.Version);
        Assert.Equal(["Мало тестів"], dto.Cons);
        await _reviews.Received(1).AddAsync(Arg.Is<Review>(r => r.Id.ToString() == dto.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_stale_expected_version_is_a_conflict_and_nothing_is_saved()
    {
        var review = TestData.Review();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var handler = new UpdateReviewCommandHandler(_reviews, TestData.CreateMapper(), NullLogger<UpdateReviewCommandHandler>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new UpdateReviewCommand(review.Id.ToString(), 3, null, null, null, ExpectedVersion: 5), default));

        await _reviews.DidNotReceive().UpdateAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_changes_the_review_and_saves_it()
    {
        var review = TestData.Review();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var handler = new UpdateReviewCommandHandler(_reviews, TestData.CreateMapper(), NullLogger<UpdateReviewCommandHandler>.Instance);

        var dto = await handler.Handle(new UpdateReviewCommand(review.Id.ToString(), 3, "Новий текст", null, null, ExpectedVersion: 1), default);

        Assert.Equal(3, dto.Rating);
        Assert.Equal("Новий текст", dto.Text);
        await _reviews.Received(1).UpdateAsync(review, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_or_deleted_review_is_not_found()
    {
        var deleted = TestData.Review();
        deleted.Delete();
        _reviews.GetByIdAsync(deleted.Id, Arg.Any<CancellationToken>()).Returns(deleted);
        var handler = new DeleteReviewCommandHandler(_reviews, NullLogger<DeleteReviewCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteReviewCommand(deleted.Id.ToString(), null), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteReviewCommand(ObjectId.GenerateNewId().ToString(), null), default));
    }

    [Fact]
    public async Task Repository_conflict_propagates_from_the_handler()
    {
        var review = TestData.Review();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        _reviews.UpdateAsync(review, Arg.Any<CancellationToken>()).Returns(_ => throw new ConflictException("modified"));
        var handler = new AddCommentCommandHandler(_reviews, TestData.CreateMapper(), NullLogger<AddCommentCommandHandler>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new AddCommentCommand(review.Id.ToString(), 1, "instructor", "Андрій", "Дякую"), default));
    }

    [Fact]
    public async Task Add_comment_returns_review_with_the_comment()
    {
        var review = TestData.Review();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var handler = new AddCommentCommandHandler(_reviews, TestData.CreateMapper(), NullLogger<AddCommentCommandHandler>.Instance);

        var dto = await handler.Handle(new AddCommentCommand(review.Id.ToString(), 1, "Instructor", "Андрій", "Дякую"), default);

        var comment = Assert.Single(dto.Comments);
        Assert.Equal("instructor", comment.Author.Role);
    }

    [Fact]
    public async Task Change_status_resolves_a_question()
    {
        var question = TestData.Question();
        _discussions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        var handler = new ChangeDiscussionStatusCommandHandler(_discussions, TestData.CreateMapper(),
            NullLogger<ChangeDiscussionStatusCommandHandler>.Instance);

        var dto = await handler.Handle(new ChangeDiscussionStatusCommand(question.Id.ToString(), "resolved", null), default);

        Assert.Equal("resolved", dto.Status);
        await _discussions.Received(1).UpdateAsync(question, Arg.Any<CancellationToken>());
    }
}
