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
    private readonly int _windowSize;
    private readonly double _accuracyUs;
    private readonly double _validityUs;
    private readonly Queue<TcoSample> _samples = new();

    public TcoEstimator(int windowSize = 5, double accuracyUs = 1000, double validityUs = 10000)
    {
        if (windowSize < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "At least two samples are needed for a fit.");
        }

        _windowSize = windowSize;
        _accuracyUs = accuracyUs;
        _validityUs = validityUs;
    }

    public int SampleCount => _samples.Count;

    public TcoCoefficients? Coefficients { get; private set; }

    public double LastDeviationUs { get; private set; }

    /// <summary>Adds a sample; once the window is full, evaluates a candidate fit.</summary>
    public TcoUpdateOutcome AddSample(TcoSample sample)
    {
        _samples.Enqueue(sample);
        while (_samples.Count > _windowSize)
        {
            _samples.Dequeue();
        }

        if (_samples.Count < _windowSize)
        {
            // Not enough samples yet: seed coefficients so early reads work.
            Coefficients ??= Fit();
            return TcoUpdateOutcome.Accepted;
        }

        var candidate = Fit();
        if (Coefficients is null)
        {
            Coefficients = candidate;
            LastDeviationUs = 0;
            return TcoUpdateOutcome.Accepted;
        }

        // Deviation = how far the candidate maps the newest OBT from the active model.
        LastDeviationUs = Math.Abs(candidate.ToUtcUs(sample.ObtUs) - Coefficients.ToUtcUs(sample.ObtUs));
        if (LastDeviationUs <= _accuracyUs)
        {
            Coefficients = candidate;
            return TcoUpdateOutcome.Accepted;
        }

        if (LastDeviationUs <= _validityUs)
        {
            Coefficients = candidate;
            return TcoUpdateOutcome.AcceptedWithWarning;
        }

        return TcoUpdateOutcome.Retained;
    }

    /// <summary>Sets coefficients manually (operator override).</summary>
    public void SetCoefficients(TcoCoefficients coefficients)
    {
        Coefficients = coefficients;
        LastDeviationUs = 0;
    }

    private TcoCoefficients Fit()
    {
        // Ordinary least squares with the window origin subtracted for stability.
        var samples = _samples.ToArray();
        long obtOrigin = samples[0].ObtUs;
        long utcOrigin = samples[0].UtcUs;

        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        int n = samples.Length;
        foreach (var sample in samples)
        {
            double x = sample.ObtUs - obtOrigin;
            double y = sample.UtcUs - utcOrigin;
            sx += x;
            sy += y;
            sxx += x * x;
            sxy += x * y;
        }

        double denominator = n * sxx - sx * sx;
        double gradient = Math.Abs(denominator) < 1e-9 ? 1.0 : (n * sxy - sx * sy) / denominator;
        double offsetRelative = (sy - gradient * sx) / n;

        // Convert the origin-relative fit back to absolute microseconds.
        double offset = utcOrigin + offsetRelative - gradient * obtOrigin;
        return new TcoCoefficients(gradient, offset, obtOrigin);
    }
}

/// <summary>A one-way time-of-flight interval over an earth-received-time window.</summary>
public sealed record TofInterval(long ErtStartUs, long ErtStopUs, double DelaySeconds);

/// <summary>Time-of-flight interval set with default fallback (L2-TIM-003).</summary>
public sealed class TofIntervalSet
{
    private readonly List<TofInterval> _intervals = [];

    public IReadOnlyList<TofInterval> Intervals => _intervals;

    public void Add(TofInterval interval)
    {
        _intervals.RemoveAll(i => i.ErtStartUs == interval.ErtStartUs);
        _intervals.Add(interval);
        _intervals.Sort((a, b) => a.ErtStartUs.CompareTo(b.ErtStartUs));
    }

    public bool Remove(long ertStartUs) => _intervals.RemoveAll(i => i.ErtStartUs == ertStartUs) > 0;

    /// <summary>Delay for an ERT, or the default when no interval covers it.</summary>
    public double DelayFor(long ertUs, double defaultDelaySeconds)
    {
        foreach (var interval in _intervals)
        {
            if (ertUs >= interval.ErtStartUs && ertUs <= interval.ErtStopUs)
            {
                return interval.DelaySeconds;
            }
        }

        return defaultDelaySeconds;
    }
}
