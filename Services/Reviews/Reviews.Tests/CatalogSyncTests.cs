using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Reviews.Application.Caching;
using Reviews.Application.Catalog.Commands.SyncCourseTitle;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Tests;

/// <summary>Лаб. 08: ідемпотентне оновлення courseTitle за подією course.renamed.</summary>
public class CatalogSyncTests
{
    private readonly ICatalogSyncStore _store = Substitute.For<ICatalogSyncStore>();
    private readonly IReviewsCacheInvalidator _cache = Substitute.For<IReviewsCacheInvalidator>();
    private readonly SyncCourseTitleCommandHandler _handler;

    private static readonly SyncCourseTitleCommand Command =
        new(Guid.NewGuid(), "CourseRenamedIntegrationEvent", "reviews.catalog-sync", 1, "C# з нуля", DateTime.UtcNow);

    public CatalogSyncTests()
        => _handler = new SyncCourseTitleCommandHandler(_store, _cache, NullLogger<SyncCourseTitleCommandHandler>.Instance);

    [Fact]
    public async Task Duplicate_event_changes_nothing()
    {
        _store.IsProcessedAsync(Command.EventId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.Equal("duplicate", result.Outcome);
        await _store.DidNotReceiveWithAnyArgs().UpdateCourseTitleAsync(default, default!, default);
    }

    [Fact]
    public async Task Older_event_does_not_overwrite_newer_title_but_is_marked_processed()
    {
        _store.TryAdvanceCourseTitleAsync(1, Command.Title, Command.OccurredAt, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.Equal("stale", result.Outcome);
        await _store.DidNotReceiveWithAnyArgs().UpdateCourseTitleAsync(default, default!, default);
        await _store.Received(1).MarkProcessedAsync(Command.EventId, Command.EventType, Command.Consumer, Command.OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_event_updates_reviews_invalidates_cache_then_marks_processed()
    {
        _store.TryAdvanceCourseTitleAsync(1, Command.Title, Command.OccurredAt, Arg.Any<CancellationToken>()).Returns(true);
        _store.UpdateCourseTitleAsync(1, Command.Title, Arg.Any<CancellationToken>()).Returns((3L, 1L));

        var result = await _handler.Handle(Command, CancellationToken.None);

        Assert.Equal(new CourseTitleSyncResult("applied", 3, 1), result);
        Received.InOrder(() =>
        {
            _store.UpdateCourseTitleAsync(1, Command.Title, Arg.Any<CancellationToken>());
            _cache.InvalidateCourseAsync(1, "CourseRenamed", Arg.Any<CancellationToken>());
            _store.MarkProcessedAsync(Command.EventId, Command.EventType, Command.Consumer, Command.OccurredAt, Arg.Any<CancellationToken>());
        });
    }
}
