using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Domain.Entities;

namespace Courses.Bll.Mapping;

public class CategoryProfile : Profile
{
    public CategoryProfile()
    {
        CreateMap<Category, CategoryWithCoursesDto>()
            .ForCtorParam(nameof(CategoryWithCoursesDto.Courses), options => options.MapFrom(category =>
                category.CourseCategories.OrderByDescending(link => link.AddedAt).ToList()));

        CreateMap<CourseCategory, CategoryCourseDto>()
            .ForCtorParam(nameof(CategoryCourseDto.Title), options => options.MapFrom(link => link.Course.Title))
            .ForCtorParam(nameof(CategoryCourseDto.Slug), options => options.MapFrom(link => link.Course.Slug))
            .ForCtorParam(nameof(CategoryCourseDto.Price), options => options.MapFrom(link => link.Course.Price))
            .ForCtorParam(nameof(CategoryCourseDto.Level), options => options.MapFrom(link => link.Course.Level.ToString()))
            .ForCtorParam(nameof(CategoryCourseDto.IsPublished), options => options.MapFrom(link => link.Course.IsPublished))
            .ForCtorParam(nameof(CategoryCourseDto.InstructorName), options => options.MapFrom(link => link.Course.Instructor.FullName));
    }
}
