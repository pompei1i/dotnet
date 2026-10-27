using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Tests;

/// <summary>Бізнес-правила сутностей і Value Objects — без жодного звернення до бази.</summary>
public class DomainTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(10)]
    public void Rating_outside_1_to_5_throws_DomainException(int value)
    {
        var exception = Assert.Throws<DomainException>(() => TestData.Review(rating: value));
        Assert.Equal("Rating must be between 1 and 5", exception.Message);
    }

    [Fact]
    public void New_review_starts_at_version_1_with_trimmed_text_and_distinct_pros()
    {
        var review = new Review(1, "  C#  ", new Author(7, " Олена "), new Rating(4), "   ",
            pros: ["Практика", "практика", " Docker "]);

        Assert.Equal(1, review.Version);
        Assert.Equal("C#", review.CourseTitle);
        Assert.Equal("Олена", review.Author.DisplayName);
        Assert.Null(review.Text);                               // порожній текст — відгук «лише оцінка»
        Assert.Equal(["Практика", "Docker"], review.Pros);
        Assert.Null(review.UpdatedAt);
    }

    [Fact]
    public void Value_objects_are_equal_by_value()
    {
        Assert.Equal(new Author(1, "Олена"), new Author(1, "Олена"));
        Assert.Equal(new Author(1, "Олена").GetHashCode(), new Author(1, "Олена").GetHashCode());
        Assert.True(new Rating(5) == new Rating(5));
        Assert.NotEqual(new Author(1, "Олена"), new Author(2, "Олена"));
        Assert.NotEqual<object>(new Rating(1), new Author(1, "1"));
    }

    [Fact]
    public void Value_objects_validate_in_constructor()
    {
        Assert.Throws<DomainException>(() => new Author(0, "Олена"));
        Assert.Throws<DomainException>(() => new Author(1, " "));
        Assert.Throws<DomainException>(() => new Participant(1, ParticipantRole.Student, ""));
    }

    [Fact]
    public void Comment_is_added_to_the_embedded_array_and_touches_the_review()
    {
        var review = TestData.Review();

        var comment = review.AddComment(TestData.Instructor, "  Дякую!  ");

        Assert.Single(review.Comments);
        Assert.Equal("Дякую!", comment.Text);
        Assert.NotNull(review.UpdatedAt);
    }

    [Fact]
    public void Review_accepts_at_most_50_comments()
    {
        var review = TestData.Review();
        for (var i = 0; i < Review.MaxComments; i++)
            review.AddComment(TestData.Student, $"Коментар {i}");

        var exception = Assert.Throws<DomainException>(() => review.AddComment(TestData.Student, "Ще один"));
        Assert.Contains("at most 50 comments", exception.Message);
    }

    [Fact]
    public void Deleted_review_cannot_be_edited_commented_or_deleted_again()
    {
        var review = TestData.Review();
        review.Delete();

        Assert.True(review.IsDeleted);
        Assert.Throws<DomainException>(() => review.Edit(new Rating(3), "x", null, null));
        Assert.Throws<DomainException>(() => review.AddComment(TestData.Student, "x"));
        Assert.Throws<DomainException>(review.Delete);
    }

    [Fact]
    public void Pros_list_is_limited()
    {
        var tooMany = Enumerable.Range(1, Review.MaxHighlights + 1).Select(i => $"Перевага {i}");
        Assert.Throws<DomainException>(() => new Review(1, "C#", new Author(1, "A"), new Rating(5), null, pros: tooMany));
    }

    [Fact]
    public void BumpVersion_increments_version()
    {
        var review = TestData.Review();
        review.BumpVersion();
        Assert.Equal(2, review.Version);
    }

    [Fact]
    public void Only_an_instructor_can_publish_an_announcement()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Discussion(1, "C#", "Оголошення", DiscussionType.Announcement, TestData.Student));
        Assert.Equal("Only an instructor can publish an announcement", exception.Message);

        var announcement = new Discussion(1, "C#", "Оголошення", DiscussionType.Announcement, TestData.Instructor);
        Assert.False(announcement.IsPinned);
    }

    [Fact]
    public void New_question_is_open_has_lowercase_distinct_tags_and_no_pin_flag()
    {
        var question = TestData.Question();

        Assert.Equal(DiscussionStatus.Open, question.Status);
        Assert.Equal(["csharp", "plan"], question.Tags);
        Assert.Null(question.IsPinned);
        Assert.Equal(question.CreatedAt, question.UpdatedAt);
    }

    [Fact]
    public void Discussion_status_transitions_follow_the_rules()
    {
        var question = TestData.Question();

        question.ChangeStatus(DiscussionStatus.Resolved);
        Assert.Equal(DiscussionStatus.Resolved, question.Status);
        Assert.Throws<DomainException>(() => question.ChangeStatus(DiscussionStatus.Resolved));

        question.ChangeStatus(DiscussionStatus.Closed);
        Assert.Throws<DomainException>(() => question.ChangeStatus(DiscussionStatus.Closed));

        question.ChangeStatus(DiscussionStatus.Open);
        Assert.Equal(DiscussionStatus.Open, question.Status);
        Assert.Throws<DomainException>(() => question.ChangeStatus(DiscussionStatus.Open));
    }

    [Fact]
    public void Announcement_cannot_be_resolved_and_question_cannot_be_pinned()
    {
        var announcement = new Discussion(1, "C#", "Оголошення", DiscussionType.Announcement, TestData.Instructor);
        Assert.Throws<DomainException>(announcement.Resolve);
        Assert.Throws<DomainException>(() => TestData.Question().Pin());
    }
}
