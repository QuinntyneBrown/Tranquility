using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.EndToEnd;

/// <summary>
/// L1 end-to-end demonstration on the real TLS Kestrel fixture: UDP telemetry
/// in -> WebSocket parameters out, and command issue -> queue accept -> CLTU
/// on the wire -> CLCW back -> ordered history stages via the API. Exercises
/// every subsystem on one path (the L1 demonstration acceptance).
/// </summary>
[Collection(RealServerCollection.Name)]
public sealed class FullSliceDemoTests(SecureKestrelFixture fixture)
{
    [Fact]
    public async Task Telemetry_flows_to_websocket_and_a_command_completes_over_the_uplink()
    {
        using var http = fixture.CreateClient();
        var token = await TokenAsync(http, TestConfig.AdminUser, TestConfig.AdminPassword);
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Discover the UDP ingest port.
        using var linksDoc = JsonDocument.Parse(await http.GetStringAsync($"/api/links/{TestConfig.Instance}"));
        var port = linksDoc.RootElement.GetProperty("links").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == "tm-in").GetProperty("boundPort").GetInt32();

        // Subscribe to parameters over wss.
        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("json");
        socket.Options.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            cert is not null && cert.GetCertHashString() == fixture.Certificate.GetCertHashString();
        await socket.ConnectAsync(
            new UriBuilder(fixture.BaseAddress) { Scheme = "wss", Path = "/api/websocket" }.Uri, CancellationToken.None);
        await using var ws = new WsTestClient(socket);

        await ws.SendJsonAsync("parameters", WsTestClient.ParameterOptions("/SampleSat/Temperature"));
        using var reply = await ws.ReceiveJsonOfTypeAsync("reply");

        // UDP telemetry in.
        using (var udp = new System.Net.Sockets.UdpClient())
        {
            await udp.SendAsync(SpacePackets.HeaderDecodeTests.GoldenPacket, "127.0.0.1", port);
        }

        // Parameter out over the WebSocket.
        using var update = await ws.ReceiveJsonOfTypeAsync("parameters", TimeSpan.FromSeconds(15));
        var value = update.RootElement.GetProperty("data").GetProperty("values").EnumerateArray()
            .Single(v => v.GetProperty("id").GetProperty("name").GetString() == "/SampleSat/Temperature");
        Assert.Equal(31.2, value.GetProperty("engValue").GetProperty("doubleValue").GetDouble(), precision: 6);

        // Command issue -> accept -> uplink -> COP-1 drains via CLCW.
        var issue = await http.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { mode = "SCIENCE" } });
        Assert.True(issue.IsSuccessStatusCode, $"issue returned {(int)issue.StatusCode}");
        using var issued = JsonDocument.Parse(await issue.Content.ReadAsStringAsync());
        var commandId = issued.RootElement.GetProperty("id").GetString();

        var accept = await http.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{commandId}:accept", null);
        Assert.True(accept.IsSuccessStatusCode);

        await Eventually.Async(async () =>
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync(
                $"/api/cop1/{TestConfig.Instance}/tc-out/status"));
            return doc.RootElement.GetProperty("vS").GetInt32() >= 1
                && doc.RootElement.GetProperty("sentQueueDepth").GetInt32() == 0;
        }, "COP-1 uplink completes end-to-end", TimeSpan.FromSeconds(15));

        // History shows ordered lifecycle stages through SENT.
        using var historyDoc = JsonDocument.Parse(await http.GetStringAsync($"/api/archive/{TestConfig.Instance}/commands"));
        var entry = historyDoc.RootElement.GetProperty("entries").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == commandId);
        var stages = entry.GetProperty("attributes").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "ISSUED", "QUEUED", "RELEASED", "SENT" },
            stages.Where(s => s is "ISSUED" or "QUEUED" or "RELEASED" or "SENT").ToArray());
    }

    private static async Task<string> TokenAsync(HttpClient http, string user, string password)
    {
        var response = await http.PostAsJsonAsync("/auth/token", new { username = user, password });
        Assert.True(response.IsSuccessStatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
