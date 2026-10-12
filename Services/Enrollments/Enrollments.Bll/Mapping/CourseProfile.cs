using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Domain.Models;

namespace Enrollments.Bll.Mapping;

public class CourseProfile : Profile
{
    public CourseProfile()
    {
        CreateMap<Course, CourseDto>();
        CreateMap<CourseSales, CourseSalesDto>();
    }
}
