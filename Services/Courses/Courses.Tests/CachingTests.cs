using System.Diagnostics.Metrics;
using Courses.Bll.Caching;
using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Courses.Dal;
using Courses.Dal.Interfaces;
using Courses.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Courses.Tests;

/// <summary>Дворівневий кеш поверх справжнього MemoryCache і MemoryDistributedCache (замість Redis).</summary>
internal sealed record TestCache(TwoLevelCache Cache, MemoryCache L1, IDistributedCache L2)
{
    public static TestCache Create(IDistributedCache? l2 = null, bool enabled = true)
    {
        var l1 = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000, TrackStatistics = true });
        l2 ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var meters = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        var cache = new TwoLevelCache(l1, l2, new CacheMetrics(meters, l1),
            Options.Create(new CachingOptions { Enabled = enabled }), NullLogger<TwoLevelCache>.Instance);
        return new TestCache(cache, l1, l2);
    }
}

public class CachingTests
{
    private static readonly CacheEntryPolicy Policy = CachePolicies.CourseCard;

    [Fact]
    public async Task Second_read_is_served_from_L1_without_calling_the_database()
    {
        var cache = TestCache.Create().Cache;
        var calls = 0;

        for (var i = 0; i < 3; i++)
            Assert.Equal("value", await cache.GetOrCreateAsync("k", _ => { calls++; return Task.FromResult("value"); }, Policy));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task L1_miss_falls_back_to_L2_and_refills_L1()
    {
        var test = TestCache.Create();
        await test.Cache.GetOrCreateAsync("k", _ => Task.FromResult("from-db"), Policy);
        test.L1.Remove("k");   // інший екземпляр сервісу: L1 порожній, Redis спільний

        var value = await test.Cache.GetOrCreateAsync<string>("k", _ => throw new InvalidOperationException("db must not be called"), Policy);

        Assert.Equal("from-db", value);
        Assert.True(test.L1.TryGetValue("k", out _));
    }

    [Fact]
    public async Task Invalidation_removes_both_levels()
    {
        var test = TestCache.Create();
        await test.Cache.GetOrCreateAsync("k", _ => Task.FromResult("old"), Policy);

        await test.Cache.InvalidateAsync("Test", ["k"]);

        Assert.False(test.L1.TryGetValue("k", out _));
        Assert.Null(await test.L2.GetAsync("k"));
        Assert.Equal("new", await test.Cache.GetOrCreateAsync("k", _ => Task.FromResult("new"), Policy));
    }

    [Fact]
    public async Task Unavailable_redis_does_not_break_reads()
    {
        var redis = Substitute.For<IDistributedCache>();
        redis.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("redis is down"));
        redis.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("redis is down"));
        var cache = TestCache.Create(redis).Cache;

        var value = await cache.GetOrCreateAsync("k", _ => Task.FromResult("from-db"), Policy);

        Assert.Equal("from-db", value);
    }

    [Fact]
    public async Task Disabled_cache_always_calls_the_database()
    {
        var cache = TestCache.Create(enabled: false).Cache;
        var calls = 0;

        await cache.GetOrCreateAsync("k", _ => Task.FromResult(++calls), Policy);
        await cache.GetOrCreateAsync("k", _ => Task.FromResult(++calls), Policy);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void L1_ttl_is_shorter_than_L2_for_every_policy()
    {
        foreach (var policy in new[] { CachePolicies.CourseCard, CachePolicies.Categories, CachePolicies.CourseListPage, CachePolicies.ListVersion })
            Assert.True(policy.L1Absolute < policy.L2Absolute);
    }

    [Fact]
    public async Task Price_change_invalidates_the_course_card()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var courses = Substitute.For<ICourseRepository>();
        uow.Courses.Returns(courses);
        var course = TestData.Course();
        courses.GetWithDetailsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(course);
        courses.GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(course);
        var service = new CourseService(uow, CourseServiceTests.CreateMapper(), TestCache.Create().Cache, NullLogger<CourseService>.Instance);

        var before = await service.GetByIdAsync(course.Id);
        await service.GetByIdAsync(course.Id);                       // з кешу: до бази вдруге не звертається
        await service.ChangePriceAsync(course.Id, new ChangePriceDto { Price = before.Price + 100 });
        var after = await service.GetByIdAsync(course.Id);

        Assert.Equal(before.Price + 100, after.Price);
        await courses.Received(2).GetWithDetailsAsync(course.Id, Arg.Any<CancellationToken>());
    }
}
