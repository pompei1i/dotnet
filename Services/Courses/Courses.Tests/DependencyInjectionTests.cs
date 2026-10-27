using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Courses.Dal;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Courses.Tests;

/// <summary>Справжня конфігурація DI з Program.cs — без підставних об'єктів і без бази.</summary>
public class DependencyInjectionTests : IClassFixture<DependencyInjectionTests.Factory>
{
    public class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("Database:MigrateOnStartup", "false");
    }

    private readonly Factory _factory;

    public DependencyInjectionTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Services_unit_of_work_and_validators_resolve()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        Assert.IsType<UnitOfWork>(provider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<CourseService>(provider.GetRequiredService<ICourseService>());
        Assert.IsType<CategoryService>(provider.GetRequiredService<ICategoryService>());
        Assert.IsType<InstructorService>(provider.GetRequiredService<IInstructorService>());
        Assert.NotNull(provider.GetRequiredService<IValidator<CreateCourseDto>>());
        Assert.NotNull(provider.GetRequiredService<IValidator<CourseQuery>>());
        provider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }

    [Fact]
    public async Task Unit_of_work_and_db_context_are_scoped()
    {
        await using var first = _factory.Services.CreateAsyncScope();
        await using var second = _factory.Services.CreateAsyncScope();

        Assert.Same(first.ServiceProvider.GetRequiredService<CoursesDbContext>(),
                    first.ServiceProvider.GetRequiredService<CoursesDbContext>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                       second.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }
}
