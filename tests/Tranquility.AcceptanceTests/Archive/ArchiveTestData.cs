using System.Text.Json;
using Tranquility.AcceptanceTests.DataLinks;
using Tranquility.AcceptanceTests.Fixtures;
using Xunit;

namespace Tranquility.AcceptanceTests.Archive;

/// <summary>Shared ingest helpers for archive suites.</summary>
public static class ArchiveTestData
{
    /// <summary>Science packet with a chosen Counter value (Temperature 31.2).</summary>
    public static byte[] PacketWithCounter(ushort counter, ushort seq = 1)
    {
        var packet = (byte[])SpacePackets.HeaderDecodeTests.GoldenPacket.Clone();
        packet[2] = (byte)(0xC0 | ((seq >> 8) & 0x3F));
        packet[3] = (byte)seq;
        packet[6] = (byte)(counter >> 8);
        packet[7] = (byte)counter;
        return packet;
    }

    /// <summary>Ingests packets and waits until history shows them all.</summary>
    public static async Task IngestAsync(InProcApiFixture fixture, HttpClient client, params byte[][] packets)
    {
        var port = await LinkApi.BoundPortAsync(client);
        using var udp = new System.Net.Sockets.UdpClient();
        foreach (var packet in packets)
        {
            await udp.SendAsync(packet, "127.0.0.1", port);
        }

        await Eventually.Async(async () =>
            (await HistoryAsync(client, "/SampleSat/Counter")).Count >= packets.Length,
            $"{packets.Length} ingested packets appear in parameter history");
    }

    public static async Task<List<JsonElement>> HistoryAsync(
        HttpClient client, string parameter, string query = "")
    {
        var response = await client.GetAsync(
            $"/api/archive/{TestConfig.Instance}/parameters{parameter}{query}");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("values").EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
