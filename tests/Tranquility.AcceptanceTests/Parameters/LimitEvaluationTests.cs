using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Alarms;
using Tranquility.Core.Decommutation;
using Xunit;

namespace Tranquility.AcceptanceTests.Parameters;

/// <summary>
/// L2-PAR-002: GIVEN a parameter value that crosses a configured threshold
/// WHEN monitoring executes THEN resulting monitoring state reflects the
/// configured level.
/// </summary>
[Requirement("L2-PAR-002")]
public sealed class LimitEvaluationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(20.0, MonitoringResult.InLimits)]   // inside all bands
    [InlineData(31.0, MonitoringResult.Warning)]    // outside warning band [-10,30]
    [InlineData(45.0, MonitoringResult.Critical)]   // outside critical band [-30,40]
    [InlineData(-15.0, MonitoringResult.Warning)]
    [InlineData(-35.0, MonitoringResult.Critical)]
    public void Threshold_crossings_map_to_configured_levels(double value, MonitoringResult expected)
    {
        var ranges = new StaticAlarmRanges
        {
            WarningRange = new AlarmRange(-10, 30),
            CriticalRange = new AlarmRange(-30, 40),
        };
        Assert.Equal(expected, ranges.Evaluate(value));
    }

    [Fact]
    public void Decommutated_out_of_limits_temperature_carries_the_warning_state()
    {
        // Golden packet temperature = 31.2, outside the [-10, 30] warning band.
        var mdb = SpacePackets.ContainerRoutingTests.LoadSampleSat();
        var engine = new DecommutationEngine(mdb);
        var result = engine.Decommutate(
            SpacePackets.HeaderDecodeTests.GoldenPacket, mdb.FindContainer("/SampleSat/Root")!, T0, T0);

        var temperature = result.Values.Single(v => v.Parameter.Name == "Temperature");
        Assert.Equal(MonitoringResult.Warning, temperature.Monitoring);
    }
}
