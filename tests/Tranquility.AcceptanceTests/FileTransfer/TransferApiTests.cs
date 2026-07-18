using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.FileTransfer;

/// <summary>
/// L2-FDP-001: create -> id + initial state. L2-FDP-002: pause/resume/cancel
/// transitions. Uses the in-process loopback CFDP service so an uplink
/// transfer runs end-to-end.
/// </summary>
public sealed class TransferApiTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    private const string Service = "default";

    [Fact]
    [Requirement("L2-FDP-001")]
    public async Task Create_transfer_returns_id_and_initial_state()
    {
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);
        var content = Convert.ToBase64String(new byte[3072]);

        var response = await operatorClient.PostAsJsonAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers",
            new { direction = "UPLOAD", bucket = "out", objectName = "payload.bin", remotePath = "incoming/payload.bin", content, reliable = true });

        Assert.True(response.IsSuccessStatusCode,
            $"create returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.Contains(root.GetProperty("state").GetString(), new[] { "QUEUED", "RUNNING" });
        Assert.Equal("CFDP", root.GetProperty("transferType").GetString());
        Assert.Matches(JsonApiAssert.Rfc3339Utc(), root.GetProperty("startTime").GetString()!);
    }

    [Fact]
    [Requirement("L2-FDP-001")]
    public async Task Uplink_transfer_completes_and_writes_the_file()
    {
        using var admin = await fixture.AdminClientAsync();
        var payload = new byte[5000];
        new Random(11).NextBytes(payload);

        var id = await CreateAsync(admin, payload, reliable: true);
        await Eventually.Async(async () => await StateAsync(admin, id) == "COMPLETED",
            "reliable uplink transfer reaches COMPLETED", TimeSpan.FromSeconds(20));

        using var doc = JsonDocument.Parse(await admin.GetStringAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers/{id}"));
        Assert.Equal(payload.Length, doc.RootElement.GetProperty("totalSize").GetInt64());
        Assert.Equal(payload.Length, doc.RootElement.GetProperty("sizeTransferred").GetInt64());
    }

    [Fact]
    [Requirement("L2-FDP-002")]
    public async Task Pause_resume_and_cancel_transition_transfer_state()
    {
        using var admin = await fixture.AdminClientAsync();
        // A large payload keeps the transfer in flight long enough to pause.
        var id = await CreateAsync(admin, new byte[2_000_000], reliable: true, paused: true);

        Assert.Equal("PAUSED", await StateAsync(admin, id));

        var resume = await admin.PostAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers/{id}:resume", null);
        Assert.True(resume.IsSuccessStatusCode);

        var cancel = await admin.PostAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers/{id}:cancel", null);
        Assert.True(cancel.IsSuccessStatusCode);
        await Eventually.Async(async () => await StateAsync(admin, id) == "CANCELLED",
            "cancelled transfer reaches CANCELLED");
    }

    [Fact]
    [Requirement("L2-FDP-002")]
    public async Task Transfer_control_requires_the_file_transfer_privilege()
    {
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);
        var response = await observer.PostAsJsonAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers",
            new { direction = "UPLOAD", bucket = "out", objectName = "x", remotePath = "incoming/x", content = "" });
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    internal static async Task<string> CreateAsync(HttpClient client, byte[] payload, bool reliable, bool paused = false)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers",
            new
            {
                direction = "UPLOAD",
                bucket = "out",
                objectName = "payload.bin",
                remotePath = "incoming/payload.bin",
                content = Convert.ToBase64String(payload),
                reliable,
                paused,
            });
        Assert.True(response.IsSuccessStatusCode,
            $"create returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    internal static async Task<string?> StateAsync(HttpClient client, string id)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/filetransfer/{TestConfig.Instance}/{Service}/transfers/{id}"));
        return doc.RootElement.GetProperty("state").GetString();
    }
}
