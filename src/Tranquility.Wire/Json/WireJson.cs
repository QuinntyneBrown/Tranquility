using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranquility.Wire.Json;

/// <summary>
/// RFC 3339 UTC timestamp encoding for every wire time field (L2-API-005):
/// <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>, always Zulu.
/// </summary>
public sealed class Rfc3339UtcConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>Documented JSON wire conventions shared by server and tooling.</summary>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new Rfc3339UtcConverter());
        // Enum states travel as UPPERCASE strings (e.g. "RUNNING").
        options.Converters.Add(new JsonStringEnumConverter(new UpperCaseNamingPolicy()));
        return options;
    }

    private sealed class UpperCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToUpperInvariant();
    }
}
