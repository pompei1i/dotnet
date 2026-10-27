using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Application.Common;

namespace Reviews.Infrastructure.Persistence.Serializers;

/// <summary>
/// Перерахування зберігаються рядками в нижньому регістрі: role: "student", status: "open" —
/// саме такі значення дозволяє валідатор $jsonSchema з Лаб. 01.
/// </summary>
public sealed class LowerCaseEnumSerializer<TEnum> : SerializerBase<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var text = context.Reader.ReadString();
        return EnumText.IsValid<TEnum>(text)
            ? EnumText.Parse<TEnum>(text)
            : throw new FormatException($"'{text}' is not a valid {typeof(TEnum).Name}");
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
        => context.Writer.WriteString(EnumText.From(value));
}
