using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus;

/// <summary>
/// Єдиний формат повідомлень для всіх сервісів: JSON (System.Text.Json), camelCase, enum — рядком,
/// дати — ISO 8601 в UTC. Однакові правила для publisher і consumer; JSON можуть читати й сервіси не на .NET.
/// </summary>
public static class EventBusJson
{
    public const string ContentType = "application/json";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // Невідомі поля нової версії схеми ігноруються — старий споживач читає нову подію
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
}
