using MongoDB.Bson;
using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Знімок останнього повідомлення теми (lastMessage): список тем показується одним запитом,
/// без звернення до колекції messages. messageId веде до повного повідомлення.
/// </summary>
public sealed class MessagePreview : ValueObject
{
    public const int MaxPreviewLength = 200;

    public ObjectId MessageId { get; }

    public string AuthorName { get; }

    public string TextPreview { get; }

    public DateTime CreatedAt { get; }

    public MessagePreview(ObjectId messageId, string authorName, string textPreview, DateTime createdAt)
    {
        MessageId = messageId;
        AuthorName = Guard.Text(authorName, "AuthorName", Author.MaxNameLength);
        var text = Guard.Text(textPreview, "TextPreview", int.MaxValue);
        TextPreview = text.Length <= MaxPreviewLength ? text : text[..MaxPreviewLength];
        CreatedAt = createdAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MessageId;
        yield return AuthorName;
        yield return TextPreview;
        yield return CreatedAt;
    }
}
