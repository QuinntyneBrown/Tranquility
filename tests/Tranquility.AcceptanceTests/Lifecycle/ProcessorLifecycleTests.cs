using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Archive;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Lifecycle;

/// <summary>
/// L2-LIF-002: GIVEN processor lifecycle requests WHEN create/edit/delete/list
/// methods are executed THEN processor state reflects requested operation.
/// </summary>
[Requirement("L2-LIF-002")]
public sealed class ProcessorLifecycleTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Create_edit_delete_and_list_reflect_the_requested_operations()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin, ArchiveTestData.PacketWithCounter(51, 31));

        // list: the realtime processor is always present
        var initial = await ListAsync(admin);
        Assert.Contains(initial, p => p.GetProperty("name").GetString() == "realtime"
            && p.GetProperty("type").GetString() == "realtime");

        // create
        var create = await admin.PostAsJsonAsync($"/api/processors/{TestConfig.Instance}", new
        {
            name = "rp1",
            type = "replay",
            start = DateTimeOffset.UtcNow.AddMinutes(-10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            stop = DateTimeOffset.UtcNow.AddMinutes(10).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            paused = true,
            persistent = true,
            speed = 1.0,
        });
        Assert.True(create.IsSuccessStatusCode, $"create returned {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        Assert.Contains(await ListAsync(admin), p => p.GetProperty("name").GetString() == "rp1"
            && p.GetProperty("type").GetString() == "replay");

        // edit
        var edit = await admin.PatchAsJsonAsync($"/api/processors/{TestConfig.Instance}/rp1", new { speed = 4.0 });
        Assert.True(edit.IsSuccessStatusCode, $"edit returned {(int)edit.StatusCode}");
        using var detail = JsonDocument.Parse(
            await admin.GetStringAsync($"/api/processors/{TestConfig.Instance}/rp1"));
        Assert.Equal(4.0, detail.RootElement.GetProperty("speed").GetDouble(), precision: 6);

        // delete
        var delete = await admin.DeleteAsync($"/api/processors/{TestConfig.Instance}/rp1");
        Assert.True(delete.IsSuccessStatusCode, $"delete returned {(int)delete.StatusCode}");
        Assert.DoesNotContain(await ListAsync(admin), p => p.GetProperty("name").GetString() == "rp1");
    }

    [Fact]
    public async Task Deleting_the_realtime_processor_is_a_documented_conflict()
    {
        using var admin = await fixture.AdminClientAsync();
        var response = await admin.DeleteAsync($"/api/processors/{TestConfig.Instance}/realtime");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    [Fact]
    public async Task Processor_creation_requires_the_control_processor_privilege()
    {
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);
        var response = await observer.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}", new { name = "nope", type = "replay" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    internal static async Task<List<JsonElement>> ListAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/processors"));
        return doc.RootElement.GetProperty("processors").EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
