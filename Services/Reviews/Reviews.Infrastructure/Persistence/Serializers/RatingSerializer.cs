using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence.Serializers;

/// <summary>
/// Value Object Rating зберігається звичайним цілим полем rating: 5 (як у Лаб. 01),
/// а не вкладеним документом { value: 5 }. Під час читання значення знову проходить
/// валідацію конструктора Rating.
/// </summary>
public sealed class RatingSerializer : SerializerBase<Rating>
{
    public override Rating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var value = reader.CurrentBsonType switch
        {
            BsonType.Int32 => reader.ReadInt32(),
            BsonType.Int64 => checked((int)reader.ReadInt64()),
            BsonType.Double => checked((int)reader.ReadDouble()),
            var type => throw new FormatException($"Cannot read rating from BSON {type}")
        };
        return new Rating(value);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);
}
