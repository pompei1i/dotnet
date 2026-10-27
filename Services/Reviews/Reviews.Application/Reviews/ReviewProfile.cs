using AutoMapper;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews;

/// <summary>
/// Сутність Review -> ReviewDto: ObjectId Id і DiscussionId стають рядками, Value Objects
/// Author/Participant/Rating — простими DTO, вбудований масив comments — списком CommentDto.
/// </summary>
public sealed class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Author, AuthorDto>();
        CreateMap<Participant, ParticipantDto>();
        CreateMap<Comment, CommentDto>();
        CreateMap<Review, ReviewDto>();
    }
}
