using AutoMapper;
using Courses.Bll.Dtos;
using Courses.Domain.Entities;

namespace Courses.Bll.Mapping;

public class InstructorProfile : Profile
{
    public InstructorProfile()
    {
        CreateMap<Instructor, InstructorDto>();
    }
}
