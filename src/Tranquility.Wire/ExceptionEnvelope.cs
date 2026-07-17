using System.Text.Json.Serialization;

namespace Tranquility.Wire;

/// <summary>
/// The documented error envelope (L2-API-004):
/// <c>{"exception":{"type":"...","msg":"..."}}</c> on every non-2xx response.
/// </summary>
public sealed record ExceptionInfo(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("msg")] string Msg);

public sealed record ExceptionEnvelope(
    [property: JsonPropertyName("exception")] ExceptionInfo Exception);
