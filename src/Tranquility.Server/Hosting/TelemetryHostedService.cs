using Tranquility.Application;

namespace Tranquility.Server.Hosting;

/// <summary>Starts and stops every configured data link with the host.</summary>
public sealed class TelemetryHostedService(InstanceRegistry registry) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var instance in registry.Instances)
        {
            foreach (var link in instance.Links)
            {
                await link.StartAsync(cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var instance in registry.Instances)
        {
            foreach (var link in instance.Links)
            {
                await link.StopAsync();
            }
        }
    }
}
