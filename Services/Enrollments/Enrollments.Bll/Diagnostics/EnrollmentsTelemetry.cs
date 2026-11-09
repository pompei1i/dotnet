using System.Diagnostics;

namespace Enrollments.Bll.Diagnostics;

/// <summary>
/// Власні spans бізнес-операцій сервісу. ServiceDefaults підписує OpenTelemetry на всі джерела
/// "Enrollments.*", тож ці spans з'являються в trace поруч зі spans HTTP і SQL (Dapper).
/// </summary>
public static class EnrollmentsTelemetry
{
    public const string SourceName = "Enrollments.Bll";

    public static readonly ActivitySource Source = new(SourceName);
}
