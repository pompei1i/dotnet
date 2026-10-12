using Enrollments.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

/// <summary>
/// Перетворює помилки SQL Server на доменні винятки, щоб «сирі» SQL-повідомлення не виходили
/// за межі DAL. Номери 50001–50010 — це THROW зі збережуваних процедур Лаб. 01
/// (db/p1/procedures.sql); 2601 і 2627 — порушення UNIQUE або PRIMARY KEY.
/// </summary>
internal static class SqlExceptionTranslator
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <returns>Доменний виняток або null, якщо помилка не є бізнесовою (тоді вона піде далі як є).</returns>
    public static Exception? ToDomainException(SqlException exception)
    {
        // Процедура може додати до своєї помилки ще службові (наприклад 266 про лічильник транзакцій),
        // тому перевіряються всі помилки відповіді, а не лише перша.
        foreach (SqlError error in exception.Errors)
        {
            switch (error.Number)
            {
                case 50001:   // Order not found
                case 50002:   // Student not found
                    return new NotFoundException(error.Message, exception);

                case 50008:   // Invalid discount
                    return new ValidationException(error.Message, exception);

                case 50003:   // Status transition not allowed
                case 50004:   // Concurrency conflict
                case 50005:   // Order is not editable
                case 50006:   // Course not available
                case 50007:   // Course already in the order
                case 50009:   // Empty order cannot be paid
                case 50010:   // Order cannot be deleted
                    return new BusinessConflictException(error.Message, exception);

                case UniqueIndexViolation:
                case UniqueConstraintViolation:
                    return new BusinessConflictException("Record with the same key already exists", exception);
            }
        }

        return null;
    }
}
