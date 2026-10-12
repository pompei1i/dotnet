using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Domain.Models;

namespace Enrollments.Bll.Mapping;

public class StudentProfile : Profile
{
    public StudentProfile()
    {
        CreateMap<Student, StudentDto>();

        // MemberList.Source: перевіряється, що кожне поле вхідного DTO має куди записатися;
        // службові поля моделі (Id, аудит) DTO не заповнює — їх виставляє сервіс або база.
        CreateMap<CreateStudentDto, Student>(MemberList.Source);
        CreateMap<UpdateStudentDto, Student>(MemberList.Source);
    }
}
