using Courses.Bll.Dtos;
using Courses.Bll.Events;
using Courses.Bll.Services;
using Courses.Dal;
using Courses.Dal.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Courses.Tests;

/// <summary>Лаб. 08: подія про зміну курсу публікується лише ПІСЛЯ успішного збереження і несе повний знімок.</summary>
public class EventPublishingTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly ICourseEventPublisher _events = Substitute.For<ICourseEventPublisher>();
    private readonly CourseService _service;

    public EventPublishingTests()
    {
        _uow.Courses.Returns(_courses);
        _service = new CourseService(_uow, CourseServiceTests.CreateMapper(), TestCache.Create().Cache, _events, NullLogger<CourseService>.Instance);
    }

    [Fact]
    public async Task Price_change_publishes_event_with_old_and_new_price_after_save()
    {
        var course = TestData.Course(price: 1200m);
        _courses.GetByIdAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        await _service.ChangePriceAsync(course.Id, new ChangePriceDto { Price = 1500m });

        Received.InOrder(() =>
        {
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _events.PriceChangedAsync(new CourseSnapshot(course.Id, course.Title, 1500m, false), 1200m, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Failed_save_publishes_nothing()
    {
        var course = TestData.Course(price: 1200m);
        _courses.GetByIdAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ChangePriceAsync(course.Id, new ChangePriceDto { Price = 1500m }));

        await _events.DidNotReceiveWithAnyArgs().PriceChangedAsync(default!, default, default);
    }

    [Fact]
    public async Task Rename_publishes_new_title_and_unchanged_title_publishes_nothing()
    {
        var course = TestData.Course(title: "C# для початківців");
        _courses.GetByIdAsync(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        await _service.RenameAsync(course.Id, new ChangeTitleDto { Title = "C# з нуля" });
        await _service.RenameAsync(course.Id, new ChangeTitleDto { Title = "C# з нуля" });

        await _events.Received(1).RenamedAsync(
            Arg.Is<CourseSnapshot>(snapshot => snapshot.Title == "C# з нуля"), "C# для початківців", Arg.Any<CancellationToken>());
    }
}
