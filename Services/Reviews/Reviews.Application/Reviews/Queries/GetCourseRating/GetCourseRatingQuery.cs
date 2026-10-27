using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetCourseRating;

/// <summary>Середня оцінка курсу й розподіл оцінок — aggregation pipeline ($match + $group).</summary>
public sealed record GetCourseRatingQuery(long CourseId) : IQuery<CourseRatingDto>;
