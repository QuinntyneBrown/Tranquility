using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Core.Alarms;

/// <summary>
/// Deterministic in-memory alarm state tracking. Raises, updates, and clears
/// alarms from monitored parameter values. No I/O dependencies (L2-QLT-002).
/// Implements: L2-PAR-002, L2-PAR-003.
/// Source: OMG XTCE 1.3 (alarm semantics); externally observable alarm lifecycle
/// per the External API documentation package (URL redacted).
/// </summary>
public sealed class AlarmStateTracker
{
    private readonly Dictionary<string, ActiveAlarm> _active = new(StringComparer.Ordinal);
    private int _nextSequence;

    public IReadOnlyCollection<ActiveAlarm> ActiveAlarms => _active.Values;

    /// <summary>
    /// Processes a monitored parameter value and returns the resulting alarm
    /// transition, or null when no state change occurred.
    /// </summary>
    public AlarmTransition? Process(ParameterValue value)
    {
        string key = value.Parameter.QualifiedName;
        bool inViolation = value.Monitoring >= MonitoringResult.Watch;

        if (_active.TryGetValue(key, out var existing))
        {
            if (!inViolation)
            {
                _active.Remove(key);
                return new AlarmTransition(AlarmTransitionKind.Cleared, existing with { ClearValue = value });
            }

            var updated = existing with
            {
                Severity = value.Monitoring > existing.Severity ? value.Monitoring : existing.Severity,
                MostRecentValue = value,
                ViolationCount = existing.ViolationCount + 1,
            };
            _active[key] = updated;
            return updated.Severity != existing.Severity
                ? new AlarmTransition(AlarmTransitionKind.SeverityIncreased, updated)
                : null;
        }

        if (!inViolation)
        {
            return null;
        }

        var raised = new ActiveAlarm(
            SequenceNumber: _nextSequence++,
            Parameter: value.Parameter,
            Severity: value.Monitoring,
            TriggerValue: value,
            MostRecentValue: value,
            ViolationCount: 1,
            ClearValue: null);
        _active[key] = raised;
        return new AlarmTransition(AlarmTransitionKind.Raised, raised);
    }
}

public enum AlarmTransitionKind
{
    Raised,
    SeverityIncreased,
    Cleared,
}

public sealed record AlarmTransition(AlarmTransitionKind Kind, ActiveAlarm Alarm);

public sealed record ActiveAlarm(
    int SequenceNumber,
    Parameter Parameter,
    MonitoringResult Severity,
    ParameterValue TriggerValue,
    ParameterValue MostRecentValue,
    int ViolationCount,
    ParameterValue? ClearValue);
