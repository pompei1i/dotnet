using AutoMapper;
using MongoDB.Bson;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Common.Mappings;

/// <summary>
/// Перетворення типів, спільні для всіх профілів: ObjectId -> рядок (у JSON не потрапляє сирий BSON),
/// Value Object Rating -> число, перерахування -> рядки в нижньому регістрі, як у документах.
/// </summary>
public sealed class CommonProfile : Profile
{
    public CommonProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
        CreateMap<Rating, int>().ConvertUsing(rating => rating.Value);
        CreateMap<ParticipantRole, string>().ConvertUsing(role => EnumText.From(role));
        CreateMap<DiscussionType, string>().ConvertUsing(type => EnumText.From(type));
        CreateMap<DiscussionStatus, string>().ConvertUsing(status => EnumText.From(status));
    }
}
