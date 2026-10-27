using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

/// <summary>
/// Увесь конвеєр ASP.NET Core і MediatR (behaviors, валідатори, обробники, ProblemDetails) —
/// справжній; підставні лише репозиторії та інтерфейси читання, тож MongoDB не потрібна.
/// Створення індексів і сідінг під час старту вимкнено.
/// </summary>
public class ApiTests : IClassFixture<ApiTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public IReviewRepository Reviews { get; } = Substitute.For<IReviewRepository>();
        public IReviewQueries ReviewQueries { get; } = Substitute.For<IReviewQueries>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("MongoDb:CreateIndexesOnStartup", "false");
            builder.UseSetting("MongoDb:SeedOnStartup", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReviewRepository>();
                services.RemoveAll<IReviewQueries>();
                services.AddSingleton(Reviews);
                services.AddSingleton(ReviewQueries);
            });
        }
    }

    private static readonly object ValidReview = new
    {
        courseId = 1, courseTitle = "C# для початківців", studentId = 7, authorName = "Олена Коваль",
        rating = 5, text = "Корисно", pros = new[] { "Практика" }
    };

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Reviews.ClearReceivedCalls();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_returns_201_with_string_ObjectId_in_Location_and_ETag()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews", ValidReview);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;
        Assert.Matches("^[0-9a-f]{24}$", id);
        Assert.Equal($"/api/reviews/{id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Rating_10_returns_400_and_the_handler_never_runs()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews", new
        {
            courseId = 1, courseTitle = "C#", studentId = 7, authorName = "Олена", rating = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Rating", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
        await _factory.Reviews.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Missing_fields_are_reported_by_fluentvalidation_all_at_once()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews", new { rating = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "CourseId", "CourseTitle", "StudentId", "AuthorName" })
            Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [Fact]
    public async Task Invalid_ObjectId_returns_400_and_unknown_id_returns_404()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/reviews/123")).StatusCode);

        var missing = await _client.GetAsync($"/api/reviews/{ObjectId.GenerateNewId()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Stale_If_Match_returns_409()
    {
        var review = TestData.Review();
        review.BumpVersion();                                       // у базі вже версія 2
        _factory.Reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/reviews/{review.Id}")
        {
            Content = JsonContent.Create(new { rating = 3 })
        };
        request.Headers.IfMatch.ParseAdd("\"1\"");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _factory.Reviews.DidNotReceive().UpdateAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_key_from_the_unique_index_returns_409()
    {
        _factory.Reviews.AddAsync(Arg.Is<Review>(r => r.Author.StudentId == 8), Arg.Any<CancellationToken>())
            .ThrowsAsync(MongoErrors.DuplicateKey());

        var response = await _client.PostAsJsonAsync("/api/reviews", new
        {
            courseId = 1, courseTitle = "C#", studentId = 8, authorName = "Олена", rating = 5
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Duplicate key", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unavailable_database_returns_503_with_retry_after()
    {
        _factory.ReviewQueries.GetCourseRatingAsync(99, Arg.Any<CancellationToken>()).ThrowsAsync(MongoErrors.ConnectionFailed());

        var response = await _client.GetAsync("/api/courses/99/rating");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.RetryAfter!.ToString());
    }

    [Fact]
    public async Task Domain_rule_violation_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/discussions", new
        {
            courseId = 1, courseTitle = "C#", title = "Оголошення", type = "announcement",
            authorId = 3, authorRole = "student", authorName = "Софія"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Business rule violated", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_returns_pagination_headers()
    {
        _factory.ReviewQueries.GetListAsync(Arg.Any<ReviewListCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new Application.Common.Models.PagedResult<ReviewListItemDto>([], 41, 2, 20));

        var response = await _client.GetAsync("/api/reviews?page=2&courseId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("41", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("3", response.Headers.GetValues("X-Total-Pages").Single());
        await _factory.ReviewQueries.Received().GetListAsync(
            Arg.Is<ReviewListCriteria>(c => c.Page == 2 && c.CourseId == 1 && c.Descending), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Swagger_lists_all_endpoints()
    {
        var swagger = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("/api/reviews", paths);
        Assert.Contains("/api/reviews/{id}", paths);
        Assert.Contains("/api/reviews/{id}/comments", paths);
        Assert.Contains("/api/discussions/{id}/status", paths);
        Assert.Contains("/api/courses/{courseId}/rating", paths);
    }

    /// <summary>Винятки драйвера, які інакше виникають лише на справжньому сервері.</summary>
    internal static class MongoErrors
    {
        private static ConnectionId Connection()
            => new(new ServerId(new ClusterId(), new System.Net.DnsEndPoint("localhost", 27017)));

        public static MongoWriteException DuplicateKey()
        {
            // Конструктор WriteError внутрішній — так його створює сам драйвер
            var writeError = (WriteError)typeof(WriteError)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single()
                .Invoke([ServerErrorCategory.DuplicateKey, 11000, "E11000 duplicate key error", new BsonDocument()]);
            return new MongoWriteException(Connection(), writeError, null, null);
        }

        public static MongoConnectionException ConnectionFailed() => new(Connection(), "connection refused");
    }
}
