using Tranquility.Application;
using Tranquility.Application.Processing;

namespace Tranquility.Server.Hosting;

/// <summary>
/// Starts every configured data link and each instance's realtime processor
/// with the host, and stops them with it.
/// </summary>
public sealed class TelemetryHostedService(
    InstanceRegistry registry,
    SubscriptionHub hub,
    TimeProvider time,
    Tranquility.Application.Abstractions.IArchive archive) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var instance in registry.Instances)
        {
            foreach (var link in instance.Links)
            {
                await link.StartAsync(cancellationToken);
            }

            var processor = new RealtimeProcessor(instance, hub, time, archive);
            instance.AttachProcessor(processor);
            processor.Start();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var instance in registry.Instances)
        {
            instance.Processor?.Stop();
            foreach (var link in instance.Links)
            {
                await link.StopAsync();
            }
        }
    }
}
