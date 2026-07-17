using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>Shared wire-level assertions for the documented API conventions.</summary>
public static partial class JsonApiAssert
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,9})?Z$")]
    public static partial Regex Rfc3339Utc();

    /// <summary>
    /// Asserts the documented error envelope
    /// <c>{"exception":{"type":"...","msg":"..."}}</c> (L2-API-004).
    /// </summary>
    public static async Task<(string Type, string Msg)> IsErrorEnvelopeAsync(HttpResponseMessage response)
    {
        Assert.False(response.IsSuccessStatusCode,
            "Expected a non-2xx response carrying the exception envelope.");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = ParseOrFail(body, response);
        Assert.True(doc.RootElement.TryGetProperty("exception", out var exception),
            $"Error body lacks 'exception' property: {body}");
        var type = exception.GetProperty("type").GetString();
        var msg = exception.GetProperty("msg").GetString();
        Assert.False(string.IsNullOrWhiteSpace(type), "exception.type must be non-empty");
        Assert.False(string.IsNullOrWhiteSpace(msg), "exception.msg must be non-empty");
        return (type!, msg!);
    }

    /// <summary>
    /// Walks a JSON document asserting every *Time-named string field is
    /// RFC 3339 UTC (L2-API-005).
    /// </summary>
    public static void AllTimestampsAreRfc3339Utc(JsonElement element, string path = "$")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = $"{path}.{property.Name}";
                    if (property.Name.EndsWith("Time", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString()!;
                        Assert.True(Rfc3339Utc().IsMatch(value),
                            $"Timestamp field {childPath} is not RFC 3339 UTC: '{value}'");
                    }

                    AllTimestampsAreRfc3339Utc(property.Value, childPath);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AllTimestampsAreRfc3339Utc(item, $"{path}[{index++}]");
                }

                break;
        }
    }

    private static JsonDocument ParseOrFail(string body, HttpResponseMessage response)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            Assert.Fail($"Non-2xx response ({(int)response.StatusCode}) body is not JSON: '{body}'");
            throw; // unreachable
        }
    }
}
