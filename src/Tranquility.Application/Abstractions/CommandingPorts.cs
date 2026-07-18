using Tranquility.Core.Cop1;

namespace Tranquility.Application.Abstractions;

/// <summary>
/// The TC uplink sink: receives radiated CLTUs and supplies CLCW reports back
/// to COP-1. The loopback adapter (tests) and a real ground-station adapter
/// both implement it. Acceptance tests observe the recording it exposes.
/// </summary>
public interface IUplinkLink : ILink
{
    /// <summary>Radiates one CLTU carrying a TC frame; returns its FECF-valid TC frame bytes.</summary>
    void Radiate(byte[] cltu);

    /// <summary>Raised when the peer returns a CLCW report.</summary>
    event Action<Clcw>? ClcwReceived;

    /// <summary>CLTUs radiated so far (observation surface for acceptance tests).</summary>
    IReadOnlyList<byte[]> RadiatedCltus { get; }
}
