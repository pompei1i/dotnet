using AutoMapper;
using Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Discussions.Queries.GetDiscussionsList;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Mapping;

/// <summary>
/// HTTP-контракти -> команди й запити MediatR. Значення з маршруту ({id}) і заголовка If-Match
/// контролер додає після мапінгу через with, тож тут вони лише заповнюються порожніми.
/// </summary>
public sealed class RequestProfile : Profile
{
    public RequestProfile()
    {
        CreateMap<ReviewListRequest, GetReviewsListQuery>();
        CreateMap<CreateReviewRequest, CreateReviewCommand>();
        CreateMap<UpdateReviewRequest, UpdateReviewCommand>()
            .ForCtorParam(nameof(UpdateReviewCommand.Id), o => o.MapFrom(_ => string.Empty))
            .ForCtorParam(nameof(UpdateReviewCommand.ExpectedVersion), o => o.MapFrom(_ => (int?)null));
        CreateMap<AddCommentRequest, AddCommentCommand>()
            .ForCtorParam(nameof(AddCommentCommand.ReviewId), o => o.MapFrom(_ => string.Empty));

        CreateMap<DiscussionListRequest, GetDiscussionsListQuery>();
        CreateMap<CreateDiscussionRequest, CreateDiscussionCommand>();
        CreateMap<ChangeDiscussionStatusRequest, ChangeDiscussionStatusCommand>()
            .ForCtorParam(nameof(ChangeDiscussionStatusCommand.Id), o => o.MapFrom(_ => string.Empty))
            .ForCtorParam(nameof(ChangeDiscussionStatusCommand.ExpectedVersion), o => o.MapFrom(_ => (int?)null));
    }
}
