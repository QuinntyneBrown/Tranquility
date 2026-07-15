using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tranquility.Application;
using Tranquility.Infrastructure.Links;

namespace Tranquility.Server.Tests;

/// <summary>
/// Boots the real server host with an ephemeral UDP ingest port.
/// Verification method: Demonstration (end-to-end slice per IMPLEMENTATION-PLAN).
/// </summary>
public sealed class TranquilityWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Tranquility:UdpPort", "0");
    }

    /// <summary>The CCSDS space packet used across the test suite (APID 100, seq 5).</summary>
    public static readonly byte[] GoldenPacket =
    [
        0x00, 0x64, 0xC0, 0x05, 0x00, 0x07,
        0x01, 0x02,             // Counter = 258
        0x40, 0x02,             // Temperature raw = 1024 (eng 31.2, WARNING), Mode = 2 (SCIENCE)
        0x3F, 0xC0, 0x00, 0x00, // BusVoltage = 1.5f
    ];

    /// <summary>Waits for the UDP link to bind, then returns its port.</summary>
    public async Task<int> GetUdpPortAsync(CancellationToken cancellationToken)
    {
        var registry = Services.GetRequiredService<InstanceRegistry>();
        var link = (UdpPacketLink)registry.Links[0];
        while (link.BoundPort == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(25, cancellationToken);
        }

        return link.BoundPort;
    }

    public async Task SendPacketAsync(byte[] packet, CancellationToken cancellationToken)
    {
        int port = await GetUdpPortAsync(cancellationToken);
        using var udp = new UdpClient();
        await udp.SendAsync(packet, "127.0.0.1", port, cancellationToken);
    }
}
