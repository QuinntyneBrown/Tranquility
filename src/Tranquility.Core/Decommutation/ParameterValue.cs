using Tranquility.Core.Alarms;
using Tranquility.Core.Mdb;

namespace Tranquility.Core.Decommutation;

/// <summary>
/// A decommutated parameter value: raw (wire) value, calibrated engineering
/// value, timestamps, and monitoring result.
/// Implements: L2-PAR-001. Source: OMG XTCE 1.3; value semantics per the
/// External API documentation package (URL redacted).
/// </summary>
public sealed record ParameterValue(
    Parameter Parameter,
    object RawValue,
    object EngValue,
    DateTimeOffset GenerationTime,
    DateTimeOffset AcquisitionTime,
    MonitoringResult Monitoring = MonitoringResult.Disabled)
{
    /// <summary>Engineering value as a double when numeric, otherwise null.</summary>
    public double? EngValueAsDouble => EngValue switch
    {
        double d => d,
        long l => l,
        ulong u => u,
        _ => null,
    };
}
