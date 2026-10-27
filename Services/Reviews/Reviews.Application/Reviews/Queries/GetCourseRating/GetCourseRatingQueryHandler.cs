using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetCourseRating;

public sealed class GetCourseRatingQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetCourseRatingQuery, CourseRatingDto>
{
    public Task<CourseRatingDto> Handle(GetCourseRatingQuery query, CancellationToken ct)
        => queries.GetCourseRatingAsync(query.CourseId, ct);
}
