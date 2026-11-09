using System.Diagnostics;

namespace Courses.Bll.Diagnostics;

/// <summary>
/// Власні spans бізнес-операцій каталогу. ServiceDefaults підписує OpenTelemetry на всі джерела
/// "Courses.*", тож ці spans з'являються в trace поруч зі spans HTTP і SQL (EF Core).
/// </summary>
public static class CoursesTelemetry
{
    public const string SourceName = "Courses.Bll";

    public static readonly ActivitySource Source = new(SourceName);
}
