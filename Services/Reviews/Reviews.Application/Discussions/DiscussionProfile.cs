using AutoMapper;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Discussions;

/// <summary>Сутність Discussion -> DiscussionDto; вбудований lastMessage — MessagePreviewDto.</summary>
public sealed class DiscussionProfile : Profile
{
    public DiscussionProfile()
    {
        CreateMap<MessagePreview, MessagePreviewDto>();
        CreateMap<Discussion, DiscussionDto>();
    }
}
