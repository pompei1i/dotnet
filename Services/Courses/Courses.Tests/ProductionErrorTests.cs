using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Courses.Bll.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Courses.Tests;

/// <summary>Поза Development клієнт не бачить тексту несподіваної помилки — лише traceId.</summary>
public class ProductionErrorTests : IClassFixture<ProductionErrorTests.ProductionFactory>
{
    public class ProductionFactory : WebApplicationFactory<Program>
    {
        public ICourseService Courses { get; } = Substitute.For<ICourseService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICourseService>();
                services.AddSingleton(Courses);
            });
        }
    }

    private readonly ProductionFactory _factory;

    public ProductionErrorTests(ProductionFactory factory) => _factory = factory;

    [Fact]
    public async Task Unexpected_error_hides_details_outside_development()
    {
        _factory.Courses.GetByIdAsync(13, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Login failed for user 'sa'"));

        var response = await _factory.CreateClient().GetAsync("/api/courses/13");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Login failed", body);
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.False(problem.TryGetProperty("exception", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
    }
}
