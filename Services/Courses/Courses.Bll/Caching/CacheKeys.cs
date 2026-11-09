using System.Security.Cryptography;
using System.Text;
using Courses.Bll.Dtos;

namespace Courses.Bll.Caching;

/// <summary>
/// Передбачувана схема ключів: "{сервіс}:{сутність}:{ідентифікатор}". Префікс "courses:" ізолює ключі
/// каталогу від ключів інших сервісів у спільному Redis, а за ключем завжди можна сказати,
/// що саме треба видалити після запису.
/// </summary>
public static class CacheKeys
{
    public const string Prefix = "courses:";

    /// <summary>Картка курсу (CourseDetailsDto) — і для REST, і для gRPC.</summary>
    public static string Course(long id) => $"{Prefix}course:{id}";

    /// <summary>Довідник категорій з кількістю курсів.</summary>
    public const string Categories = Prefix + "categories:all";

    /// <summary>
    /// Ключ-версія для масової інвалідації: сторінок списку з різними фільтрами може бути безліч,
    /// тож їх не перелічують, а змінюють версію — старі ключі просто перестають читатися й зникають за TTL.
    /// </summary>
    public const string ListVersion = Prefix + "list:version";

    /// <summary>Сторінка каталогу: версія списків + стабільний хеш усіх параметрів запиту.</summary>
    public static string CourseListPage(string version, CourseQuery query, int page, int pageSize)
    {
        var parameters = string.Join('|',
            query.Search?.Trim().ToLowerInvariant(), query.Level?.ToLowerInvariant(),
            query.CategoryId, query.InstructorId, query.MinPrice, query.MaxPrice, query.IsPublished,
            query.SortBy.ToLowerInvariant(), query.SortDir.ToLowerInvariant(), page, pageSize);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parameters)))[..16].ToLowerInvariant();
        return $"{Prefix}list:v{version}:{hash}";
    }
}
