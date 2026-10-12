using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Dal;
using Enrollments.Domain.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Enrollments.Tests;

/// <summary>
/// Перевіряє справжню конфігурацію DI з Program.cs — без підставних об'єктів.
/// З'єднання з базою тут не відкривається: UnitOfWork відкриває його ліниво, під час першого запиту.
/// </summary>
public class DependencyInjectionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Real_services_and_unit_of_work_resolve_from_the_container()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        Assert.IsType<UnitOfWork>(provider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<StudentService>(provider.GetRequiredService<IStudentService>());
        Assert.IsType<CourseService>(provider.GetRequiredService<ICourseService>());
        Assert.IsType<OrderService>(provider.GetRequiredService<IOrderService>());
    }

    [Fact]
    public async Task Unit_of_work_is_scoped_one_instance_per_request()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    [Fact]
    public void Mapper_registered_in_program_maps_models_to_dtos()
    {
        var mapper = factory.Services.GetRequiredService<IMapper>();

        mapper.ConfigurationProvider.AssertConfigurationIsValid();

        var dto = mapper.Map<CourseDto>(new Course { Id = 1, Title = "C#", Price = 1200m, IsAvailable = true });
        Assert.Equal("C#", dto.Title);
    }
}
