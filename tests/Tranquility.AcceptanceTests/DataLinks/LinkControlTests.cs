using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.DataLinks;

/// <summary>
/// L2-LNK-002: GIVEN an enabled link WHEN disable operation is requested THEN
/// link state changes to disabled and is reflected in subsequent status
/// queries (and ingest actually stops).
/// </summary>
[Requirement("L2-LNK-002")]
public sealed class LinkControlTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Disable_reflects_in_status_and_stops_ingest_then_enable_resumes()
    {
        using var admin = await fixture.AdminClientAsync();

        var port = await LinkApi.BoundPortAsync(admin);

        // Disable: status flips and traffic no longer counts.
        var disable = await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:disable", null);
        Assert.True(disable.IsSuccessStatusCode, $":disable returned {(int)disable.StatusCode}");
        Assert.True((await LinkApi.GetLinkAsync(admin, "tm-in")).GetProperty("disabled").GetBoolean());

        var before = await LinkApi.DataInCountAsync(admin);
        await SendDatagramsAsync(port, 5);
        await Task.Delay(300); // give any (incorrect) counting a chance to surface
        Assert.Equal(before, await LinkApi.DataInCountAsync(admin));

        // Enable: traffic counts again.
        var enable = await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:enable", null);
        Assert.True(enable.IsSuccessStatusCode);
        Assert.False((await LinkApi.GetLinkAsync(admin, "tm-in")).GetProperty("disabled").GetBoolean());

        await SendDatagramsAsync(port, 3);
        await Eventually.Async(async () => await LinkApi.DataInCountAsync(admin) >= before + 3,
            "dataInCount increments after re-enable");
    }

    [Fact]
    public async Task Reset_counters_zeroes_the_link_counters()
    {
        using var admin = await fixture.AdminClientAsync();
        var port = await LinkApi.BoundPortAsync(admin);

        await SendDatagramsAsync(port, 2);
        await Eventually.Async(async () => await LinkApi.DataInCountAsync(admin) >= 2, "traffic counted");

        var reset = await admin.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:resetCounters", null);
        Assert.True(reset.IsSuccessStatusCode, $":resetCounters returned {(int)reset.StatusCode}");
        Assert.Equal(0, await LinkApi.DataInCountAsync(admin));
    }

    [Fact]
    public async Task Link_control_requires_the_control_links_privilege()
    {
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);
        var response = await observer.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:disable", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    internal static async Task SendDatagramsAsync(int port, int count)
    {
        using var udp = new UdpClient();
        for (var i = 0; i < count; i++)
        {
            // A valid single-packet datagram.
            await udp.SendAsync(SpaceDataLink.FrameBuilder.Packet(100, [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]),
                "127.0.0.1", port);
        }
    }
}

internal static class LinkApi
{
    public static async Task<JsonElement> GetLinkAsync(HttpClient client, string name)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/links/{TestConfig.Instance}"));
        return doc.RootElement.GetProperty("links").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == name).Clone();
    }

    public static async Task<long> DataInCountAsync(HttpClient client) =>
        (await GetLinkAsync(client, "tm-in")).GetProperty("dataInCount").GetInt64();

    public static async Task<int> BoundPortAsync(HttpClient client)
    {
        var link = await GetLinkAsync(client, "tm-in");
        var port = link.GetProperty("boundPort").GetInt32();
        Assert.True(port > 0, "Link must expose its bound UDP port");
        return port;
    }
}
