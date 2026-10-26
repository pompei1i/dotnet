using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Bll.Mapping;
using Courses.Bll.Services;
using Courses.Dal;
using Courses.Dal.Interfaces;
using Courses.Domain.Entities;
using Courses.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Courses.Tests;

/// <summary>Unit-тести BLL: Unit of Work і репозиторії замінено підставними об'єктами.</summary>
public class CourseServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IInstructorRepository _instructors = Substitute.For<IInstructorRepository>();
    private readonly CourseService _service;

    internal static IMapper CreateMapper()
        => new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<CourseProfile>();
            cfg.AddProfile<CategoryProfile>();
            cfg.AddProfile<InstructorProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

    public CourseServiceTests()
    {
        _uow.Courses.Returns(_courses);
        _uow.Categories.Returns(_categories);
        _uow.Instructors.Returns(_instructors);

        _instructors.GetByIdAsync(1L, Arg.Any<CancellationToken>()).Returns(TestData.Instructor(1));
        _categories.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns([TestData.Category(1), TestData.Category(3, "Бази даних")]);

        _service = new CourseService(_uow, CreateMapper(), NullLogger<CourseService>.Instance);
    }

    private static CreateCourseDto NewCourse() => new()
    {
        InstructorId = 1,
        Title = "Entity Framework Core на практиці",
        Slug = "ef-core-in-practice",
        Price = 1500m,
        Level = "Intermediate",
        Description = "Code First, Fluent API, міграції",
        DurationHours = 18m,
        CategoryIds = [1, 3],
        PrimaryCategoryId = 3
    };

    [Fact]
    public void Mapping_configuration_is_valid()
        => CreateMapper().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public async Task Create_with_taken_slug_throws_conflict_and_saves_nothing()
    {
        _courses.ExistsBySlugAsync("ef-core-in-practice", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(NewCourse()));

        await _courses.DidNotReceive().AddAsync(Arg.Any<Course>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_for_unknown_instructor_throws_not_found()
    {
        var input = NewCourse();
        input.InstructorId = 99;

        var error = await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.CreateAsync(input));
        Assert.Equal("Instructor 99 not found", error.Message);
    }

    [Fact]
    public async Task Create_with_unknown_category_throws_not_found()
    {
        var input = NewCourse();
        input.CategoryIds = [1, 3, 42];

        var error = await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.CreateAsync(input));
        Assert.Contains("42", error.Message);
    }

    [Fact]
    public async Task Create_adds_course_with_detail_and_primary_category_and_saves_once()
    {
        var created = await _service.CreateAsync(NewCourse());

        await _courses.Received(1).AddAsync(
            Arg.Is<Course>(course =>
                course.Slug == "ef-core-in-practice" &&
                course.Level == CourseLevel.Intermediate &&
                course.Detail != null &&
                course.CourseCategories.Count == 2 &&
                course.CourseCategories.Single(link => link.IsPrimary).CategoryId == 3),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        Assert.Equal("Бази даних", created.Categories.First().Name);   // основна категорія першою
        Assert.True(created.Categories.First().IsPrimary);
    }

    [Fact]
    public async Task ChangePrice_of_missing_course_throws_not_found()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.ChangePriceAsync(77, new ChangePriceDto { Price = 100m }));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_without_lessons_is_rejected_by_the_entity()
    {
        var draft = TestData.Course(id: 6);
        draft.SetDetail("Курс у розробці, опис уже є", null, 20m, false);
        _courses.GetWithDetailsAsync(6, Arg.Any<CancellationToken>()).Returns(draft);

        await Assert.ThrowsAsync<DomainException>(() => _service.PublishAsync(6));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetList_clamps_page_size_and_reports_total_from_count_query()
    {
        _courses.CountAsync(Arg.Any<Ardalis.Specification.ISpecification<Course>>(), Arg.Any<CancellationToken>()).Returns(23);
        _courses.ListAsync(Arg.Any<Ardalis.Specification.ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns([TestData.Course()]);

        var page = await _service.GetListAsync(new CourseQuery { Page = 2, PageSize = 500 });

        Assert.Equal(23, page.TotalCount);
        Assert.Equal(CourseQuery.MaxPageSize, page.PageSize);
        Assert.Equal(1, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.False(page.HasNext);
    }
}
