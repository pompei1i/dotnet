using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Коментар до відгуку — елемент вбудованого масиву comments. Окремого Id не має:
/// існує лише всередині свого відгуку й змінюється тільки через методи Review.
/// </summary>
public class Comment
{
    public Participant Author { get; private set; } = null!;

    public string Text { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }

    private Comment() { }   // для десеріалізації

    internal Comment(Participant author, string text, DateTime createdAt)
    {
        Author = author;
        Text = text;
        CreatedAt = createdAt;
    }
}
