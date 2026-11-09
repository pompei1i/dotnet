using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reviews.Application;
using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetCourseRating;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

/// <summary>
/// Кеш запитів через CachingBehavior і HybridCache (без Redis — лише L1). IMemoryCache з SizeLimit,
/// як у Program.cs: HybridCache має сам задавати Size записам, інакше MemoryCache кинув би виняток.
/// </summary>
public class CachingTests
{
    private readonly IReviewQueries _queries = Substitute.For<IReviewQueries>();
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();

    private ISender BuildSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache(options => options.SizeLimit = 50L * 1024 * 1024);
        services.AddSingleton(TestData.CreateMapper());
        services.AddSingleton(_queries);
        services.AddSingleton(_reviews);
        services.AddSingleton(Substitute.For<IDiscussionRepository>());
        services.AddApplication(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private static CourseRatingDto Rating(long courseId, int count)
        => new(courseId, count, 4.5, new Dictionary<string, int> { ["5"] = count });

    [Fact]
    public async Task Repeated_rating_query_is_served_from_the_cache()
    {
        _queries.GetCourseRatingAsync(1, Arg.Any<CancellationToken>()).Returns(Rating(1, 2));
        var sender = BuildSender();

        var first = await sender.Send(new GetCourseRatingQuery(1));
        var second = await sender.Send(new GetCourseRatingQuery(1));

        // HybridCache віддає десеріалізовану копію, тож порівнюємо значення, а не посилання на словник
        Assert.Equal((first.CourseId, first.ReviewsCount, first.AverageRating), (second.CourseId, second.ReviewsCount, second.AverageRating));
        Assert.Equal(first.Distribution, second.Distribution);
        await _queries.Received(1).GetCourseRatingAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_review_invalidates_the_course_rating()
    {
        _queries.GetCourseRatingAsync(1, Arg.Any<CancellationToken>()).Returns(Rating(1, 2), Rating(1, 3));
        var sender = BuildSender();

        await sender.Send(new GetCourseRatingQuery(1));
        await sender.Send(new CreateReviewCommand(1, "C# для початківців", 7, "Олена Коваль", 5, "Текст", null, null));
        var after = await sender.Send(new GetCourseRatingQuery(1));

        Assert.Equal(3, after.ReviewsCount);
        await _queries.Received(2).GetCourseRatingAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_and_student_queries_are_not_cached()
    {
        _queries.GetListAsync(Arg.Any<ReviewListCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ReviewListItemDto>([], 0, 1, 20));
        var sender = BuildSender();

        await sender.Send(new GetReviewsListQuery(CourseId: 1, Search: "linq"));
        await sender.Send(new GetReviewsListQuery(CourseId: 1, Search: "linq"));
        await sender.Send(new GetReviewsListQuery(StudentId: 7));
        await sender.Send(new GetReviewsListQuery(StudentId: 7));

        await _queries.Received(4).GetListAsync(Arg.Any<ReviewListCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Cache_keys_are_prefixed_and_l1_ttl_is_shorter()
    {
        ICacheableQuery query = new GetReviewsListQuery(CourseId: 2, SortBy: "createdAt", SortDir: "desc", Page: 1, PageSize: 3);

        Assert.Equal("reviews:list:course:2:createdat:desc:any:1:3", query.CacheKey);
        Assert.Equal(["reviews:course:2"], query.CacheTags);
        Assert.True(query.CachePolicy.Local < query.CachePolicy.Distributed);
        Assert.Equal("reviews:rating:2", ((ICacheableQuery)new GetCourseRatingQuery(2)).CacheKey);
    }
}
