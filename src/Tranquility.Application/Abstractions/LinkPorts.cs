using System.Threading.Channels;

namespace Tranquility.Application.Abstractions;

public enum LinkStatus
{
    Ok,
    Unavailable,
    Disabled,
    Failed,
}

/// <summary>An adapter-advertised custom action (L2-LNK-003).</summary>
public sealed record LinkActionSpec(string Id, string Label, bool Enabled);

/// <summary>
/// Data link port (L1-LNK-001): control-plane state, counters, custom
/// actions, and the inbound packet stream.
/// </summary>
public interface ILink
{
    string Name { get; }

    string Type { get; }

    LinkStatus Status { get; }

    bool Enabled { get; }

    long DataInCount { get; }

    long DataOutCount { get; }

    string DetailedStatus { get; }

    /// <summary>Locally bound port for socket links, when applicable.</summary>
    int? BoundPort { get; }

    IReadOnlyList<LinkActionSpec> Actions { get; }

    void Enable();

    void Disable();

    void ResetCounters();

    /// <summary>
    /// Executes an advertised action; throws <see cref="NotFoundServiceException"/>
    /// for an unknown action id. The result shape is the action's contract.
    /// </summary>
    Task<object> RunActionAsync(string actionId, CancellationToken cancellationToken);

    /// <summary>Inbound packets after any frame-layer processing.</summary>
    ChannelReader<byte[]> Packets { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}

/// <summary>Creates link adapters from configuration.</summary>
public interface ILinkFactory
{
    ILink Create(LinkOptions options);
}
