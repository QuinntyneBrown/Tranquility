using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;

namespace Tranquility.Core.Tests.Decommutation;

/// <summary>Verifies L2-PAR-002/003 alarm evaluation and state transitions.</summary>
public class AlarmTests
{
    private static readonly StaticAlarmRanges Ranges = new()
    {
        WatchRange = new AlarmRange(0, 50),
        WarningRange = new AlarmRange(-10, 60),
        CriticalRange = new AlarmRange(-20, 80),
    };

    [Theory]
    [InlineData(25.0, MonitoringResult.InLimits)]
    [InlineData(55.0, MonitoringResult.Watch)]
    [InlineData(-15.0, MonitoringResult.Warning)]
    [InlineData(100.0, MonitoringResult.Critical)]
    public void Evaluate_ReturnsMostSevereViolatedLevel(double value, MonitoringResult expected)
    {
        Assert.Equal(expected, Ranges.Evaluate(value));
    }

    [Fact]
    public void Evaluate_UnboundedSide_DoesNotViolate()
    {
        var ranges = new StaticAlarmRanges { WarningRange = new AlarmRange(null, 10) };
        Assert.Equal(MonitoringResult.InLimits, ranges.Evaluate(-1e9));
        Assert.Equal(MonitoringResult.Warning, ranges.Evaluate(10.5));
    }

    [Fact]
    public void Tracker_ViolationThenReturnToLimits_RaisesAndClears()
    {
        var tracker = new AlarmStateTracker();

        var raised = tracker.Process(Sample(70.0, MonitoringResult.Warning));
        Assert.Equal(AlarmTransitionKind.Raised, raised!.Kind);
        Assert.Single(tracker.ActiveAlarms);

        Assert.Null(tracker.Process(Sample(71.0, MonitoringResult.Warning)));

        var cleared = tracker.Process(Sample(20.0, MonitoringResult.InLimits));
        Assert.Equal(AlarmTransitionKind.Cleared, cleared!.Kind);
        Assert.Empty(tracker.ActiveAlarms);
    }

    [Fact]
    public void Tracker_EscalatingSeverity_ReportsIncrease()
    {
        var tracker = new AlarmStateTracker();
        tracker.Process(Sample(70.0, MonitoringResult.Warning));

        var escalated = tracker.Process(Sample(95.0, MonitoringResult.Critical));

        Assert.Equal(AlarmTransitionKind.SeverityIncreased, escalated!.Kind);
        Assert.Equal(MonitoringResult.Critical, escalated.Alarm.Severity);
    }

    [Fact]
    public void Tracker_InLimitsWithoutActiveAlarm_NoTransition()
    {
        var tracker = new AlarmStateTracker();
        Assert.Null(tracker.Process(Sample(10.0, MonitoringResult.InLimits)));
    }

    private static ParameterValue Sample(double value, MonitoringResult monitoring)
    {
        var type = new FloatParameterType("f", "/T/f", new FloatDataEncoding(32));
        var parameter = new Parameter("P", "/T/P", type);
        return new ParameterValue(parameter, value, value, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, monitoring);
    }
}
