using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Decommutation;
using Tranquility.Core.Mdb;
using Xunit;

namespace Tranquility.AcceptanceTests.Parameters;

/// <summary>
/// L2-PAR-001: GIVEN a raw telemetry sample with calibration metadata WHEN
/// processed THEN output contains expected engineering value.
/// </summary>
[Requirement("L2-PAR-001")]
public sealed class CalibrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Polynomial_calibration_produces_the_expected_engineering_value()
    {
        // SampleSat Temperature: eng = -20 + 0.05 * raw; raw 1024 -> 31.2
        var result = DecommutateGolden();
        var temperature = result.Values.Single(v => v.Parameter.Name == "Temperature");

        Assert.Equal(1024UL, temperature.RawValue);
        Assert.Equal(31.2, Assert.IsType<double>(temperature.EngValue), precision: 9);
    }

    [Fact]
    public void Enumeration_calibration_maps_raw_values_to_labels()
    {
        var result = DecommutateGolden();
        var mode = result.Values.Single(v => v.Parameter.Name == "Mode");

        Assert.Equal(2L, mode.RawValue);
        Assert.Equal("SCIENCE", mode.EngValue);
    }

    [Fact]
    public void Polynomial_calibrator_evaluates_higher_order_terms()
    {
        // eng = 1 + 2x + 3x^2 at x=4 -> 57
        var calibrator = new PolynomialCalibrator([1, 2, 3]);
        Assert.Equal(57, calibrator.Apply(4), precision: 12);
    }

    private static DecommutationResult DecommutateGolden()
    {
        var mdb = SpacePackets.ContainerRoutingTests.LoadSampleSat();
        var engine = new DecommutationEngine(mdb);
        return engine.Decommutate(
            SpacePackets.HeaderDecodeTests.GoldenPacket, mdb.FindContainer("/SampleSat/Root")!, T0, T0);
    }
}
