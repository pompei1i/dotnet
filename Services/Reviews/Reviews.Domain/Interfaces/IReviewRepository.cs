using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>
/// Запис відгуків. Контракт оголошено в домені, реалізацію (MongoDB Driver) — в Infrastructure.
/// Читання для API йде окремим шляхом — через IReviewQueries з проєкцією в DTO.
/// </summary>
public interface IReviewRepository
{
    /// <summary>Відгук разом із вбудованими коментарями; м'яко видалений теж повертається.</summary>
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>
    /// Чи залишав студент відгук на курс — враховуються й видалені, бо унікальний індекс
    /// { courseId, author.studentId } їх теж містить.
    /// </summary>
    Task<bool> ExistsAsync(long courseId, long studentId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>
    /// Зберігає зміни, лише якщо документ у базі досі має версію review.Version (оптимістична
    /// конкурентність); інакше — ConflictException. Після запису версія сутності зростає на 1.
    /// </summary>
    Task UpdateAsync(Review review, CancellationToken ct = default);
}
