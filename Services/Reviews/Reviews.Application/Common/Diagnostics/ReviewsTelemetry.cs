using System.Diagnostics;

namespace Reviews.Application.Common.Diagnostics;

/// <summary>
/// Власні spans команд і запитів MediatR. ServiceDefaults підписує OpenTelemetry на всі джерела
/// "Reviews.*", тож ці spans стоять у trace між span HTTP-запиту і spans команд MongoDB.
/// </summary>
public static class ReviewsTelemetry
{
    public const string SourceName = "Reviews.Application";

    public static readonly ActivitySource Source = new(SourceName);
}
