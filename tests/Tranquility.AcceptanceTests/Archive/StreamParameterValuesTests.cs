using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Archive;

/// <summary>
/// L2-ARC-002: GIVEN a replay request with start/stop times WHEN stream
/// endpoint is invoked THEN parameter data is emitted as a chunked server
/// stream.
/// </summary>
[Requirement("L2-ARC-002")]
public sealed class StreamParameterValuesTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Stream_emits_archived_values_as_progressive_ndjson_batches()
    {
        using var admin = await fixture.AdminClientAsync();
        var packets = Enumerable.Range(1, 40)
            .Select(i => ArchiveTestData.PacketWithCounter((ushort)(100 + i), (ushort)i))
            .ToArray();
        await ArchiveTestData.IngestAsync(fixture, admin, packets);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/archive/{TestConfig.Instance}:streamParameterValues")
        {
            Content = JsonContent.Create(new
            {
                ids = new[] { new { name = "/SampleSat/Counter" } },
            }),
        };
        using var response = await admin.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.True(response.IsSuccessStatusCode, $"stream endpoint returned {(int)response.StatusCode}");

        var seen = new List<long>();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        var batches = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            batches++;
            using var doc = JsonDocument.Parse(line);
            foreach (var value in doc.RootElement.GetProperty("values").EnumerateArray())
            {
                seen.Add(long.Parse(value.GetProperty("engValue").GetProperty("uint64Value").GetString()!));
            }
        }

        Assert.True(batches >= 1, "stream must consist of newline-delimited batches");
        foreach (var expected in Enumerable.Range(1, 40).Select(i => 100L + i))
        {
            Assert.Contains(expected, seen);
        }
    }

    [Fact]
    public async Task Bounded_stream_respects_start_and_stop()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(201, 50));

        using var response = await admin.PostAsJsonAsync(
            $"/api/archive/{TestConfig.Instance}:streamParameterValues",
            new
            {
                ids = new[] { new { name = "/SampleSat/Counter" } },
                start = DateTimeOffset.UtcNow.AddHours(-3).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                stop = DateTimeOffset.UtcNow.AddHours(-2).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(string.IsNullOrWhiteSpace(body),
            $"An interval before ingest must stream no values, got: {body}");
    }

    [Fact]
    public async Task Client_abandoning_the_stream_leaves_the_server_healthy()
    {
        using var admin = await fixture.AdminClientAsync();
        await ArchiveTestData.IngestAsync(fixture, admin,
            ArchiveTestData.PacketWithCounter(210, 60));

        using (var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/archive/{TestConfig.Instance}:streamParameterValues")
        {
            Content = JsonContent.Create(new { ids = new[] { new { name = "/SampleSat/Counter" } } }),
        })
        using (var response = await admin.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
        {
            // Read nothing and drop the response: cancellation must propagate.
        }

        var health = await admin.GetAsync("/api/instances");
        Assert.True(health.IsSuccessStatusCode, "server must keep serving after an abandoned stream");
    }
}
