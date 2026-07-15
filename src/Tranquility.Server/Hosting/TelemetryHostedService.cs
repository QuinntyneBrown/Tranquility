using Tranquility.Application;

namespace Tranquility.Server.Hosting;

/// <summary>
/// Hosts the telemetry pipeline: starts links, marks the instance running,
/// and runs every processor until shutdown.
/// Implements: L2-LIF-001, L2-LIF-002 (instance/processor lifecycle).
/// </summary>
public sealed class TelemetryHostedService : BackgroundService
{
    private readonly InstanceRegistry _registry;
    private readonly ILogger<TelemetryHostedService> _logger;

    public TelemetryHostedService(InstanceRegistry registry, ILogger<TelemetryHostedService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var link in _registry.Links)
        {
            await link.StartAsync(stoppingToken);
            _logger.LogInformation("Link {Link} started", link.Name);
        }

        _registry.MarkRunning();
        _logger.LogInformation("Instance {Instance} running", _registry.InstanceName);

        try
        {
            await Task.WhenAll(_registry.Processors.Select(p => p.RunAsync(stoppingToken)));
        }
        finally
        {
            foreach (var link in _registry.Links)
            {
                await link.StopAsync();
            }

            _registry.MarkOffline();
        }
    }
}
