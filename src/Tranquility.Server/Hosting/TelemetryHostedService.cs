using Tranquility.Application;
using Tranquility.Application.Abstractions;
using Tranquility.Application.Commanding;
using Tranquility.Application.Processing;
using Tranquility.Core.Cop1;

namespace Tranquility.Server.Hosting;

/// <summary>
/// Starts every configured data link, each instance's realtime processor, and
/// the commanding runtime (COP-1 per uplink link + command service) with the
/// host, and stops them with it.
/// </summary>
public sealed class TelemetryHostedService(
    InstanceRegistry registry,
    SubscriptionHub hub,
    TimeProvider time,
    IArchive archive,
    IAuditLog audit) : IHostedService
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

            // COP-1 for each uplink link; commanding uses the first uplink.
            Cop1Service? primaryCop1 = null;
            foreach (var uplink in instance.Links.OfType<IUplinkLink>())
            {
                var cop1 = new Cop1Service(uplink, new Cop1Profile());
                instance.Cop1Services[uplink.Name] = cop1;
                primaryCop1 ??= cop1;
            }

            instance.Commands = new CommandService(instance, primaryCop1, audit, time);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var instance in registry.Instances)
        {
            instance.Processor?.Stop();
            foreach (var cop1 in instance.Cop1Services.Values)
            {
                await cop1.DisposeAsync();
            }

            foreach (var link in instance.Links)
            {
                await link.StopAsync();
            }
        }
    }
}
