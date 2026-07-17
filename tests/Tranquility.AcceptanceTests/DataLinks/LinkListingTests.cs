using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.DataLinks;

/// <summary>
/// L2-LNK-001: GIVEN an instance with configured links WHEN list-links
/// endpoint is queried THEN each link includes name, disabled status,
/// counters, and detail fields.
/// </summary>
[Requirement("L2-LNK-001")]
public sealed class LinkListingTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Configured_link_is_listed_with_documented_metadata_fields()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/links/{TestConfig.Instance}");

        Assert.True(response.IsSuccessStatusCode,
            $"GET /api/links returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var link = doc.RootElement.GetProperty("links").EnumerateArray()
            .Single(l => l.GetProperty("name").GetString() == "tm-in");
        Assert.Equal("UDP", link.GetProperty("type").GetString());
        Assert.False(link.GetProperty("disabled").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(link.GetProperty("status").GetString()));
        Assert.True(link.GetProperty("dataInCount").GetInt64() >= 0);
        Assert.True(link.GetProperty("dataOutCount").GetInt64() >= 0);
        Assert.True(link.TryGetProperty("detailedStatus", out _), "detail field must be present");
        Assert.Equal(JsonValueKind.Array, link.GetProperty("actions").ValueKind);
    }

    [Fact]
    public async Task Unknown_instance_returns_404_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/links/no-such-instance");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }
}
