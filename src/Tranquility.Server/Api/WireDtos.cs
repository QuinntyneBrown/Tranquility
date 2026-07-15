using System.Text.Json;
using System.Text.Json.Serialization;
using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Server.Api;

// Wire shapes per docs/specs/TRQ-ICD-API.md (JSON only; protobuf deferred, TBD-012).
// Implements: L2-API-001 (resource families), L2-API-004 (RFC 3339 UTC timestamps).

public sealed record InstanceInfoDto(string Name, string State);

public sealed record InstancesResponse(IReadOnlyList<InstanceInfoDto> Instances);

public sealed record ProcessorInfoDto(string Instance, string Name, string Type, string State);

public sealed record ProcessorsResponse(IReadOnlyList<ProcessorInfoDto> Processors);

public sealed record LinkInfoDto(
    string Instance, string Name, string Type, string Status, bool Disabled, long DataInCount);

public sealed record LinksResponse(IReadOnlyList<LinkInfoDto> Links);

public sealed record ParameterTypeInfoDto(string EngType);

public sealed record ParameterInfoDto(string Name, string QualifiedName, ParameterTypeInfoDto Type);

public sealed record ParametersResponse(IReadOnlyList<ParameterInfoDto> Parameters);

public sealed record NamedObjectIdDto(string Name);

/// <summary>Typed value union: exactly one *Value field is populated per Type.</summary>
public sealed record ValueDto(
    string Type,
    long? Sint64Value = null,
    ulong? Uint64Value = null,
    double? DoubleValue = null,
    string? StringValue = null);

public sealed record ParameterValueDto(
    NamedObjectIdDto Id,
    ValueDto RawValue,
    ValueDto EngValue,
    DateTimeOffset GenerationTime,
    DateTimeOffset AcquisitionTime,
    string? MonitoringResult);

public static class WireMapper
{
    /// <summary>Shared serializer options: camelCase, nulls omitted, RFC 3339 UTC.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new Rfc3339UtcConverter() },
    };

    public static ValueDto ToValueDto(object value) => value switch
    {
        long l => new ValueDto("SINT64", Sint64Value: l),
        ulong u => new ValueDto("UINT64", Uint64Value: u),
        double d => new ValueDto("DOUBLE", DoubleValue: d),
        string s => new ValueDto("STRING", StringValue: s),
        _ => throw new NotSupportedException($"Unsupported value type {value.GetType().Name}."),
    };

    public static ParameterValueDto ToDto(ParameterValue value) => new(
        new NamedObjectIdDto(value.Parameter.QualifiedName),
        ToValueDto(value.RawValue),
        ToValueDto(value.EngValue),
        value.GenerationTime,
        value.AcquisitionTime,
        ToWire(value.Monitoring));

    public static ParameterInfoDto ToDto(Parameter parameter) => new(
        parameter.Name,
        parameter.QualifiedName,
        new ParameterTypeInfoDto(EngTypeOf(parameter.Type)));

    public static string? ToWire(MonitoringResult monitoring) => monitoring switch
    {
        MonitoringResult.Disabled => null,
        MonitoringResult.InLimits => "IN_LIMITS",
        MonitoringResult.Watch => "WATCH",
        MonitoringResult.Warning => "WARNING",
        MonitoringResult.Distress => "DISTRESS",
        MonitoringResult.Critical => "CRITICAL",
        MonitoringResult.Severe => "SEVERE",
        _ => null,
    };

    private static string EngTypeOf(ParameterType type) => type switch
    {
        IntegerParameterType => "integer",
        FloatParameterType => "float",
        EnumeratedParameterType => "enumeration",
        _ => "unknown",
    };
}

/// <summary>Writes timestamps as RFC 3339 UTC with a Z designator (L2-API-004).</summary>
public sealed class Rfc3339UtcConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture));
}
