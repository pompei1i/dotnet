using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>Запис тем обговорення; реалізація — в Infrastructure.</summary>
public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task AddAsync(Discussion discussion, CancellationToken ct = default);

    /// <summary>Оптимістична конкурентність — як у IReviewRepository.UpdateAsync.</summary>
    Task UpdateAsync(Discussion discussion, CancellationToken ct = default);
}
