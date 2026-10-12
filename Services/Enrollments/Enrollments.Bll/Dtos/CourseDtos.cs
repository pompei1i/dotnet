namespace Enrollments.Bll.Dtos;

public record CourseDto(long Id, string Title, decimal Price, bool IsAvailable);

public record CourseSalesDto(long CourseId, string CourseTitle, int OrdersCount, decimal Revenue);
