using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;

namespace Tranquility.Application.Cfdp;

/// <summary>Owns CFDP transfer services keyed by (instance, serviceName).</summary>
public sealed class TransferRegistry(InstanceRegistry instances, IFilestore filestore, SubscriptionHub hub, TimeProvider time)
{
    private readonly Dictionary<(string, string), TransferService> _services = new();
    private readonly Lock _gate = new();

    public TransferService Get(string instance, string serviceName)
    {
        instances.Get(instance); // 404 for unknown instances
        lock (_gate)
        {
            var key = (instance, serviceName);
            if (!_services.TryGetValue(key, out var service))
            {
                _services[key] = service = new TransferService(instance, filestore, hub, time);
            }

            return service;
        }
    }
}
