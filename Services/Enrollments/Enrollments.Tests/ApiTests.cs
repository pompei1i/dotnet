using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Domain;
using Enrollments.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace Enrollments.Tests;

/// <summary>
/// API-тести: справжній конвеєр ASP.NET Core (маршрути, валідація моделі, глобальний обробник
/// помилок, Swagger) із підставними сервісами BLL — база даних для них не потрібна.
/// </summary>
public class ApiTests : IClassFixture<ApiTests.ApiFactory>
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public IOrderService Orders { get; } = Substitute.For<IOrderService>();
        public IStudentService Students { get; } = Substitute.For<IStudentService>();
        public ICourseService Courses { get; } = Substitute.For<ICourseService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOrderService>();
                services.RemoveAll<IStudentService>();
                services.RemoveAll<ICourseService>();
                services.AddSingleton(Orders);
                services.AddSingleton(Students);
                services.AddSingleton(Courses);
            });
        }
    }

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Orders.ClearSubstitute();
        _factory.Students.ClearSubstitute();
        _client = factory.CreateClient();
    }

    private static OrderDto SampleOrder(long id) => new(
        id, 1, OrderStatuses.Submitted, new DateTime(2026, 10, 1), 1000m,
        new OrderDetailsDto("Olena Koval", "olena@example.com", "Card", null),
        [new OrderItemDto(1, "C# for beginners", 1200m, 200m, 1000m)]);

    private static object ValidOrderRequest() => new
    {
        studentId = 1,
        billingName = "Olena Koval",
        billingEmail = "olena@example.com",
        paymentMethod = "Card",
        items = new[] { new { courseId = 1, discount = 200 } }
    };

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Get_missing_order_returns_404_problem_details()
    {
        _factory.Orders.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("Order 999 not found"));

        var response = await _client.GetAsync("/api/orders/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("Order 999 not found", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Paying_already_paid_order_returns_409_problem_details()
    {
        _factory.Orders.ChangeStatusAsync(5, OrderStatuses.Paid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessConflictException("Status transition Paid -> Paid is not allowed"));

        var response = await _client.PostAsync("/api/orders/5/pay", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("Business rule conflict", problem.GetProperty("title").GetString());
        Assert.Contains("not allowed", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Domain_validation_exception_returns_400_problem_details()
    {
        _factory.Orders.CreateAsync(Arg.Any<CreateOrderDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException("Discount 5000 exceeds the price 1200 of course 1"));

        var response = await _client.PostAsJsonAsync("/api/orders", ValidOrderRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_leaking_details()
    {
        _factory.Orders.GetByIdAsync(13, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Login failed for user 'sa'"));

        var response = await _client.GetAsync("/api/orders/13");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Login failed", body);
        Assert.Equal(500, (await ReadProblemAsync(response)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Invalid_request_is_rejected_with_400_before_service_is_called()
    {
        var invalid = new
        {
            studentId = 0,                       // Range(1, ...)
            billingName = "",                    // Required
            billingEmail = "not-an-email",       // EmailAddress
            paymentMethod = "Cash",              // RegularExpression
            items = Array.Empty<object>()        // MinLength(1)
        };

        var response = await _client.PostAsJsonAsync("/api/orders", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("StudentId", out _));
        Assert.True(errors.TryGetProperty("BillingEmail", out _));
        Assert.True(errors.TryGetProperty("PaymentMethod", out _));
        Assert.True(errors.TryGetProperty("Items", out _));
        await _factory.Orders.DidNotReceive().CreateAsync(Arg.Any<CreateOrderDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_nested_item_is_rejected_with_400()
    {
        var invalid = new
        {
            studentId = 1,
            billingName = "Olena Koval",
            billingEmail = "olena@example.com",
            paymentMethod = "Card",
            items = new[] { new { courseId = 0, discount = -5 } }
        };

        var response = await _client.PostAsJsonAsync("/api/orders", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.Orders.DidNotReceive().CreateAsync(Arg.Any<CreateOrderDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_order_returns_201_with_location_header()
    {
        _factory.Orders.CreateAsync(Arg.Any<CreateOrderDto>(), Arg.Any<CancellationToken>())
            .Returns(SampleOrder(42));

        var response = await _client.PostAsJsonAsync("/api/orders", ValidOrderRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/orders/42", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, body.GetProperty("id").GetInt64());
        Assert.Equal("C# for beginners", body.GetProperty("items")[0].GetProperty("courseTitle").GetString());
    }

    [Fact]
    public async Task Checkout_returns_204_and_passes_target_status_to_service()
    {
        var response = await _client.PostAsync("/api/orders/3/checkout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.Orders.Received(1).ChangeStatusAsync(3, OrderStatuses.AwaitingPayment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_order_returns_204()
    {
        var response = await _client.DeleteAsync("/api/orders/4");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.Orders.Received(1).DeleteAsync(4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Page_out_of_range_returns_400()
    {
        var response = await _client.GetAsync("/api/students?page=0&pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.Students.DidNotReceiveWithAnyArgs().GetPagedAsync(default, default, default);
    }

    [Fact]
    public async Task Create_student_returns_201_and_row_version_as_base64()
    {
        _factory.Students.CreateAsync(Arg.Any<CreateStudentDto>(), Arg.Any<CancellationToken>())
            .Returns(new StudentDto(9, "o.brien@example.com", "Conan O'Brien", null, DateTime.UtcNow, null, [0, 0, 0, 0, 0, 0, 7, 209]));

        var response = await _client.PostAsJsonAsync("/api/students",
            new { email = "o.brien@example.com", fullName = "Conan O'Brien" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/students/9", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AAAAAAAAB9E=", body.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task Swagger_document_describes_the_endpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/orders", out _));
        Assert.True(paths.TryGetProperty("/api/orders/{id}/pay", out _));
        Assert.True(paths.TryGetProperty("/api/students/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/courses/{id}/sales", out _));
    }
}
