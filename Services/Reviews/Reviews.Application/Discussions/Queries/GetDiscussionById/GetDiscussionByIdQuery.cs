using Reviews.Application.Common.Interfaces;
using Reviews.Application.Discussions.Dtos;

namespace Reviews.Application.Discussions.Queries.GetDiscussionById;

public sealed record GetDiscussionByIdQuery(string Id) : IQuery<DiscussionDto>;
