using Tranquility.Application.Abstractions;
using Tranquility.Core.Time;

namespace Tranquility.Application.Tco;

public sealed record TcoConfig(double AccuracyUs, double ValidityUs);

public sealed record TcoServiceStatus(
    TcoCoefficients? Coefficients,
    double Deviation,
    int SampleCount,
    TcoConfig Config,
    IReadOnlyList<TofInterval> TofIntervals);

/// <summary>
/// Runtime time-correlation service for one instance/serviceName: hosts a
/// <see cref="TcoEstimator"/> and TOF interval set, exposes status, and
/// applies operator config/coefficient/interval changes (L2-TIM-001/002/003).
/// </summary>
public sealed class TcoService
{
    private readonly Lock _gate = new();
    private TcoEstimator _estimator;
    private TcoConfig _config;
    private readonly TofIntervalSet _tof = new();

    public TcoService(TcoConfig? config = null)
    {
        _config = config ?? new TcoConfig(1000, 10000);
        _estimator = new TcoEstimator(accuracyUs: _config.AccuracyUs, validityUs: _config.ValidityUs);
    }

    public TcoServiceStatus Status()
    {
        lock (_gate)
        {
            return new TcoServiceStatus(
                _estimator.Coefficients, _estimator.LastDeviationUs, _estimator.SampleCount,
                _config, _tof.Intervals.ToList());
        }
    }

    public void SetConfig(TcoConfig config)
    {
        lock (_gate)
        {
            _config = config;
            // Re-seed the estimator with the new thresholds, preserving coefficients.
            var previous = _estimator.Coefficients;
            _estimator = new TcoEstimator(accuracyUs: config.AccuracyUs, validityUs: config.ValidityUs);
            if (previous is not null)
            {
                _estimator.SetCoefficients(previous);
            }
        }
    }

    public void SetCoefficients(TcoCoefficients coefficients)
    {
        lock (_gate)
        {
            _estimator.SetCoefficients(coefficients);
        }
    }

    public void AddInterval(TofInterval interval)
    {
        lock (_gate)
        {
            _tof.Add(interval);
        }
    }

    public bool RemoveInterval(long ertStartUs)
    {
        lock (_gate)
        {
            return _tof.Remove(ertStartUs);
        }
    }

    public long? MapObtToUtc(long obtUs)
    {
        lock (_gate)
        {
            return _estimator.Coefficients?.ToUtcUs(obtUs);
        }
    }
}

/// <summary>Owns TCO services keyed by (instance, serviceName).</summary>
public sealed class TcoRegistry(InstanceRegistry instances)
{
    private readonly Dictionary<(string, string), TcoService> _services = new();
    private readonly Lock _gate = new();

    public TcoService Get(string instance, string serviceName)
    {
        instances.Get(instance); // 404 for unknown instances
        lock (_gate)
        {
            var key = (instance, serviceName);
            if (!_services.TryGetValue(key, out var service))
            {
                _services[key] = service = new TcoService();
            }

            return service;
        }
    }
}
