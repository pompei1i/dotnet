using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Discussions.Dtos;

namespace Reviews.Application.Discussions.Queries.GetDiscussionById;

public sealed class GetDiscussionByIdQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionByIdQuery, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(GetDiscussionByIdQuery query, CancellationToken ct)
        => await queries.GetByIdAsync(ObjectId.Parse(query.Id), ct)
           ?? throw new NotFoundException("Discussion", query.Id);
}
