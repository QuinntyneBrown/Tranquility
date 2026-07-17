namespace Tranquility.Core.Alarms;

/// <summary>
/// Monitoring result of a parameter value against its alarm definition.
/// Severity levels follow the XTCE static alarm range names.
/// Source: OMG XTCE 1.3 (AlarmRanges).
/// </summary>
public enum MonitoringResult
{
    Disabled = 0,
    InLimits = 1,
    Watch = 2,
    Warning = 3,
    Distress = 4,
    Critical = 5,
    Severe = 6,
}

/// <summary>
/// An inclusive numeric range; a value outside the range violates the range.
/// Unbounded sides are null.
/// Source: OMG XTCE 1.3 (FloatRange in AlarmRanges).
/// </summary>
public readonly record struct AlarmRange(double? Min, double? Max)
{
    public bool Contains(double value) =>
        (Min is not double min || value >= min) && (Max is not double max || value <= max);
}

/// <summary>
/// XTCE static alarm ranges. Each defined range states the band a value must stay
/// inside to avoid that severity; violating a range raises at least that severity.
/// Implements: L2-PAR-002. Source: OMG XTCE 1.3 (StaticAlarmRanges).
/// </summary>
public sealed class StaticAlarmRanges
{
    public AlarmRange? WatchRange { get; init; }

    public AlarmRange? WarningRange { get; init; }

    public AlarmRange? DistressRange { get; init; }

    public AlarmRange? CriticalRange { get; init; }

    public AlarmRange? SevereRange { get; init; }

    /// <summary>Evaluates a calibrated value, returning the most severe violated level.</summary>
    public MonitoringResult Evaluate(double value)
    {
        if (SevereRange is { } severe && !severe.Contains(value))
        {
            return MonitoringResult.Severe;
        }

        if (CriticalRange is { } critical && !critical.Contains(value))
        {
            return MonitoringResult.Critical;
        }

        if (DistressRange is { } distress && !distress.Contains(value))
        {
            return MonitoringResult.Distress;
        }

        if (WarningRange is { } warning && !warning.Contains(value))
        {
            return MonitoringResult.Warning;
        }

        if (WatchRange is { } watch && !watch.Contains(value))
        {
            return MonitoringResult.Watch;
        }

        return MonitoringResult.InLimits;
    }
}
