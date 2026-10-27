using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Courses.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace Courses.Tests;

/// <summary>
/// Справжній конвеєр ASP.NET Core (маршрути, DataAnnotations, FluentValidation, ProblemDetails,
/// Swagger) з підставними сервісами. Міграції під час старту вимкнено — база не потрібна.
/// </summary>
public class ApiTests : IClassFixture<ApiTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public ICourseService Courses { get; } = Substitute.For<ICourseService>();
        public ICategoryService Categories { get; } = Substitute.For<ICategoryService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICourseService>();
                services.RemoveAll<ICategoryService>();
                services.AddSingleton(Courses);
                services.AddSingleton(Categories);
            });
        }
    }

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Courses.ClearSubstitute();
        _factory.Categories.ClearSubstitute();
        _client = factory.CreateClient();
    }

    private static object ValidCourse(string slug = "ef-core-in-practice") => new
    {
        instructorId = 2,
        title = "Entity Framework Core на практиці",
        slug,
        price = 1500,
        level = "Intermediate",
        description = "Code First, Fluent API, міграції",
        durationHours = 18,
        categoryIds = new[] { 3, 1 },
        primaryCategoryId = 3
    };

    private static CourseDetailsDto CreatedCourse(long id) => new(
        id, "Entity Framework Core на практиці", "ef-core-in-practice", 1500m, "Intermediate", "uk",
        false, null, null, 0, new InstructorSummaryDto(2, "Ірина Шевченко"),
        new CourseDetailDto("Code First, Fluent API, міграції", null, 18m, true), [], []);

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Create_returns_201_with_location()
    {
        _factory.Courses.CreateAsync(Arg.Any<CreateCourseDto>(), Arg.Any<CancellationToken>()).Returns(CreatedCourse(7));

        var response = await _client.PostAsJsonAsync("/api/courses", ValidCourse());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/courses/7", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_slug_returns_409_problem_details()
    {
        _factory.Courses.CreateAsync(Arg.Any<CreateCourseDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException("Course with slug 'ef-core-in-practice' already exists"));

        var response = await _client.PostAsJsonAsync("/api/courses", ValidCourse());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Contains("already exists", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Empty_title_is_rejected_by_data_annotations_with_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/courses", new
        {
            instructorId = 2, title = "", slug = "x", price = 10, level = "Beginner",
            description = "short", durationHours = 1, categoryIds = Array.Empty<int>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Title", out _));
        Assert.True(errors.TryGetProperty("Description", out _));
        Assert.True(errors.TryGetProperty("CategoryIds", out _));
        await _factory.Courses.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Bad_slug_is_rejected_by_fluent_validation_with_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/courses", ValidCourse(slug: "EF Core!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Slug", out _));
        await _factory.Courses.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Unknown_sort_field_returns_400()
    {
        var response = await _client.GetAsync("/api/courses?sortBy=password");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadProblemAsync(response)).GetProperty("errors").TryGetProperty("SortBy", out _));
    }

    [Fact]
    public async Task List_returns_page_metadata_in_body_and_headers()
    {
        _factory.Courses.GetListAsync(Arg.Any<CourseQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CourseDto>([], TotalCount: 6, Page: 2, PageSize: 5));

        var response = await _client.GetAsync("/api/courses?page=2&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("6", response.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("2", response.Headers.GetValues("X-Total-Pages").Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        Assert.False(body.GetProperty("hasNext").GetBoolean());
        Assert.True(body.GetProperty("hasPrevious").GetBoolean());
    }

    [Fact]
    public async Task Page_size_above_limit_returns_400()
    {
        var response = await _client.GetAsync("/api/courses?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_course_returns_404_problem_details()
    {
        _factory.Courses.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException("Course", 999));

        var response = await _client.GetAsync("/api/courses/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Course 999 not found", (await ReadProblemAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Domain_rule_violation_returns_409()
    {
        _factory.Courses.PublishAsync(6, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Course needs at least one lesson before it can be published"));

        var response = await _client.PostAsync("/api/courses/6/publish", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Change_price_returns_204()
    {
        var response = await _client.PutAsJsonAsync("/api/courses/1/price", new { price = 1300 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.Courses.Received(1).ChangePriceAsync(1, Arg.Is<ChangePriceDto>(dto => dto.Price == 1300m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unexpected_error_returns_500_with_details_only_in_development()
    {
        _factory.Courses.GetByIdAsync(13, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var response = await _client.GetAsync("/api/courses/13");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("boom", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task Correlation_id_from_request_is_echoed_back()
    {
        _factory.Categories.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add("X-Correlation-ID", "demo-123");

        var response = await _client.SendAsync(request);

        Assert.Equal("demo-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Swagger_document_describes_the_endpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/courses", out _));
        Assert.True(paths.TryGetProperty("/api/courses/{id}/price", out _));
        Assert.True(paths.TryGetProperty("/api/categories/{slug}/recent-courses", out _));
        Assert.True(paths.TryGetProperty("/api/instructors/{id}/courses", out _));
    }
}
