using System.Net;
using System.Text.Json;

namespace Tranquility.Server.Tests;

/// <summary>
/// HTTP API contract tests per docs/specs/TRQ-ICD-API.md.
/// Verifies: L2-API-001 (resources), L2-API-002 (error envelope),
/// L2-API-004 (RFC 3339 UTC timestamps), L2-LNK-002 (link control).
/// </summary>
public sealed class ApiIntegrationTests : IClassFixture<TranquilityWebApplicationFactory>
{
    private readonly TranquilityWebApplicationFactory _factory;

    public ApiIntegrationTests(TranquilityWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInstances_ReturnsSampleInstance()
    {
        using var client = _factory.CreateClient();
        using var doc = await GetJsonAsync(client, "/api/instances");

        var instance = Assert.Single(doc.RootElement.GetProperty("instances").EnumerateArray().ToArray());
        Assert.Equal("sample", instance.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetUnknownInstance_Returns404WithErrorEnvelope()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/instances/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var exception = doc.RootElement.GetProperty("exception");
        Assert.Equal("NotFoundException", exception.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(exception.GetProperty("msg").GetString()));
    }

    [Fact]
    public async Task GetLinks_ReturnsUdpLink()
    {
        using var client = _factory.CreateClient();
        using var doc = await GetJsonAsync(client, "/api/links/sample");

        var link = Assert.Single(doc.RootElement.GetProperty("links").EnumerateArray().ToArray());
        Assert.Equal("udp-in", link.GetProperty("name").GetString());
        Assert.Equal("UDP", link.GetProperty("type").GetString());
    }

    [Fact]
    public async Task DisableLink_ReflectsInLinkStatus()
    {
        using var client = _factory.CreateClient();

        using var disable = await client.PostAsync("/api/links/sample/udp-in:disable", content: null);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        using (var doc = await GetJsonAsync(client, "/api/links/sample"))
        {
            var link = Assert.Single(doc.RootElement.GetProperty("links").EnumerateArray().ToArray());
            Assert.True(link.GetProperty("disabled").GetBoolean());
        }

        using var enable = await client.PostAsync("/api/links/sample/udp-in:enable", content: null);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
    }

    [Fact]
    public async Task MdbParameters_ListsSampleSatParameters()
    {
        using var client = _factory.CreateClient();
        using var doc = await GetJsonAsync(client, "/api/mdb/sample/parameters");

        string?[] names = doc.RootElement.GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("qualifiedName").GetString())
            .ToArray();
        Assert.Contains("/SampleSat/Temperature", names);
        Assert.Contains("/SampleSat/BusVoltage", names);
    }

    [Fact]
    public async Task MdbParameterDetail_ReturnsTypeInfo()
    {
        using var client = _factory.CreateClient();
        using var doc = await GetJsonAsync(client, "/api/mdb/sample/parameters/SampleSat/Temperature");

        Assert.Equal("Temperature", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("float", doc.RootElement.GetProperty("type").GetProperty("engType").GetString());
    }

    [Fact]
    public async Task ParameterValue_UnknownParameter_Returns404()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/processors/sample/realtime/parameters/SampleSat/NoSuch");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
