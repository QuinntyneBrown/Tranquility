using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Time;
using Xunit;

namespace Tranquility.AcceptanceTests.TimeCorrelation;

/// <summary>
/// Core deterministic behaviour behind L2-TIM-001/002: least-squares fit,
/// deviation policy, TOF interval management.
/// </summary>
public sealed class TcoEstimatorTests
{
    [Fact]
    [Requirement("L2-TIM-001")]
    public void Estimator_fits_a_linear_model_from_a_full_sample_window()
    {
        var estimator = new TcoEstimator(windowSize: 3);
        // utc = 1_000_000 + 1.0 * obt (gradient 1, offset 1_000_000 us)
        Assert.Equal(TcoUpdateOutcome.Accepted, estimator.AddSample(new TcoSample(0, 1_000_000)));
        Assert.Equal(TcoUpdateOutcome.Accepted, estimator.AddSample(new TcoSample(1_000_000, 2_000_000)));
        Assert.Equal(TcoUpdateOutcome.Accepted, estimator.AddSample(new TcoSample(2_000_000, 3_000_000)));

        var coefficients = estimator.Coefficients;
        Assert.NotNull(coefficients);
        Assert.Equal(1.0, coefficients.Gradient, precision: 6);
        Assert.Equal(1_000_000, coefficients.ToUtcUs(0));
        Assert.Equal(3, estimator.SampleCount);
    }

    [Fact]
    [Requirement("L2-TIM-002")]
    public void Deviation_beyond_validity_retains_the_previous_coefficients()
    {
        var estimator = new TcoEstimator(windowSize: 2, accuracyUs: 100, validityUs: 1000);
        estimator.AddSample(new TcoSample(0, 1_000_000));
        estimator.AddSample(new TcoSample(1_000_000, 2_000_000));
        var baseline = estimator.Coefficients!;

        // A wildly inconsistent sample pushes the candidate beyond validity.
        var outcome = estimator.AddSample(new TcoSample(2_000_000, 9_000_000));
        Assert.Equal(TcoUpdateOutcome.Retained, outcome);
        Assert.Equal(baseline.ToUtcUs(0), estimator.Coefficients!.ToUtcUs(0));
    }

    [Fact]
    [Requirement("L2-TIM-002")]
    public void Manual_coefficients_override_the_active_model()
    {
        var estimator = new TcoEstimator();
        estimator.SetCoefficients(new TcoCoefficients(2.0, 500_000, 0));
        Assert.Equal(2.0, estimator.Coefficients!.Gradient, precision: 9);
        Assert.Equal(500_000 + 2 * 10, estimator.Coefficients.ToUtcUs(10));
    }

    [Fact]
    [Requirement("L2-TIM-003")]
    public void Tof_interval_set_applies_covering_intervals_and_a_default()
    {
        var set = new TofIntervalSet();
        set.Add(new TofInterval(1000, 2000, 0.5));
        set.Add(new TofInterval(3000, 4000, 1.5));

        Assert.Equal(0.5, set.DelayFor(1500, defaultDelaySeconds: 0.1), precision: 9);
        Assert.Equal(1.5, set.DelayFor(3500, defaultDelaySeconds: 0.1), precision: 9);
        Assert.Equal(0.1, set.DelayFor(5000, defaultDelaySeconds: 0.1), precision: 9);

        Assert.True(set.Remove(1000));
        Assert.Equal(0.1, set.DelayFor(1500, defaultDelaySeconds: 0.1), precision: 9);
        Assert.Single(set.Intervals);
    }
}
