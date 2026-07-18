namespace Tranquility.Core.Time;

/// <summary>Linear time-correlation coefficients: utc_us = Offset + Gradient * obt_us.</summary>
public sealed record TcoCoefficients(double Gradient, double Offset, long ObtEpochUs)
{
    public long ToUtcUs(long obtUs) => (long)Math.Round(Offset + Gradient * obtUs);
}

/// <summary>One onboard-time / ground-time observation pair.</summary>
public readonly record struct TcoSample(long ObtUs, long UtcUs);

/// <summary>Deviation policy outcome when a new fit is evaluated.</summary>
public enum TcoUpdateOutcome
{
    Accepted,
    AcceptedWithWarning,
    Retained,
}

/// <summary>
/// Least-squares onboard-to-UTC time correlation over a bounded sample ring
/// (ADR-0004 option 1). Deviation policy: within accuracy promotes silently,
/// within validity promotes with a warning, beyond validity retains the prior
/// coefficients. Pure and deterministic (L1-TIM-001, L2-QLT-002).
/// </summary>
public sealed class TcoEstimator
{
    public TcoEstimator(int windowSize = 5, double accuracyUs = 1000, double validityUs = 10000)
    {
        throw new NotImplementedException();
    }

    public int SampleCount => throw new NotImplementedException();

    public TcoCoefficients? Coefficients => throw new NotImplementedException();

    public double LastDeviationUs => throw new NotImplementedException();

    /// <summary>Adds a sample; once the window is full, evaluates a candidate fit.</summary>
    public TcoUpdateOutcome AddSample(TcoSample sample)
    {
        throw new NotImplementedException();
    }

    /// <summary>Sets coefficients manually (operator override).</summary>
    public void SetCoefficients(TcoCoefficients coefficients)
    {
        throw new NotImplementedException();
    }
}

/// <summary>A one-way time-of-flight interval over an earth-received-time window.</summary>
public sealed record TofInterval(long ErtStartUs, long ErtStopUs, double DelaySeconds);

/// <summary>Time-of-flight interval set with default fallback (L2-TIM-003).</summary>
public sealed class TofIntervalSet
{
    public IReadOnlyList<TofInterval> Intervals => throw new NotImplementedException();

    public void Add(TofInterval interval) => throw new NotImplementedException();

    public bool Remove(long ertStartUs) => throw new NotImplementedException();

    /// <summary>Delay for an ERT, or the default when no interval covers it.</summary>
    public double DelayFor(long ertUs, double defaultDelaySeconds) => throw new NotImplementedException();
}
