using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Запис відгуків через MongoDB Driver; реалізує доменний IReviewRepository.</summary>
public sealed class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(Builders<Review>.Filter.Eq(r => r.Id, id)).FirstOrDefaultAsync(ct);

    /// <summary>Пошук за унікальним індексом ux_reviews_course_student.</summary>
    public Task<bool> ExistsAsync(long courseId, long studentId, CancellationToken ct = default)
        => _reviews
            .Find(Builders<Review>.Filter.Eq(r => r.CourseId, courseId)
                  & Builders<Review>.Filter.Eq(r => r.Author.StudentId, studentId))
            .AnyAsync(ct);

    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    public Task UpdateAsync(Review review, CancellationToken ct = default)
        => OptimisticConcurrency.ReplaceAsync(_reviews, review, "Review", ct);
}
