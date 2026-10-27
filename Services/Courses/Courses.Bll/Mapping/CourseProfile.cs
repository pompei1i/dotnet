using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Dal.ReadModels;
using Courses.Domain.Entities;

namespace Courses.Bll.Mapping;

public class CourseProfile : Profile
{
    public CourseProfile()
    {
        CreateMap<Course, CourseDto>()
            .ForCtorParam(nameof(CourseDto.InstructorName), options => options.MapFrom(course => course.Instructor.FullName))
            .ForCtorParam(nameof(CourseDto.Categories), options => options.MapFrom(course =>
                course.CourseCategories
                    .OrderByDescending(link => link.IsPrimary)
                    .ThenBy(link => link.Category.Name)
                    .Select(link => link.Category.Name)
                    .ToList()));

        CreateMap<Course, CourseDetailsDto>()
            .ForCtorParam(nameof(CourseDetailsDto.Lessons), options => options.MapFrom(course =>
                course.Lessons.OrderBy(lesson => lesson.OrderIndex).ToList()))
            .ForCtorParam(nameof(CourseDetailsDto.Categories), options => options.MapFrom(course =>
                course.CourseCategories.OrderByDescending(link => link.IsPrimary).ThenBy(link => link.AddedAt).ToList()));

        CreateMap<Instructor, InstructorSummaryDto>();
        CreateMap<CourseDetail, CourseDetailDto>();
        CreateMap<Lesson, LessonDto>();

        CreateMap<CourseCategory, CourseCategoryDto>()
            .ForCtorParam(nameof(CourseCategoryDto.Name), options => options.MapFrom(link => link.Category.Name))
            .ForCtorParam(nameof(CourseCategoryDto.Slug), options => options.MapFrom(link => link.Category.Slug));

        // Рядок проєкції LINQ-запиту через проміжну сутність
        CreateMap<CategoryCourseEntry, RecentCourseDto>();
    }
}
