using System.Net;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.MissionDatabase;

/// <summary>
/// L2-MDB-003: GIVEN a parameter alias mapping WHEN a client uses alias-based
/// identification THEN the same parameter is resolved as with qualified name.
/// </summary>
[Requirement("L2-MDB-003")]
public sealed class AliasResolutionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Alias_resolves_to_the_same_parameter_as_the_qualified_name()
    {
        using var client = fixture.CreateClient();

        var byName = await GetParameterAsync(client, "SampleSat/Temperature");
        var byAlias = await GetParameterAsync(client, "TEMP01");

        Assert.Equal("/SampleSat/Temperature", byName.GetProperty("qualifiedName").GetString());
        Assert.Equal(
            byName.GetProperty("qualifiedName").GetString(),
            byAlias.GetProperty("qualifiedName").GetString());

        var alias = Assert.Single(byName.GetProperty("aliases").EnumerateArray());
        Assert.Equal("MIL-STD", alias.GetProperty("namespace").GetString());
        Assert.Equal("TEMP01", alias.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unknown_parameter_name_returns_404_envelope()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync(
            $"/api/mdb/{TestConfig.Instance}/parameters/NoSuchParameter");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    private static async Task<JsonElement> GetParameterAsync(HttpClient client, string name)
    {
        var response = await client.GetAsync($"/api/mdb/{TestConfig.Instance}/parameters/{name}");
        Assert.True(response.IsSuccessStatusCode,
            $"GET parameter '{name}' returned {(int)response.StatusCode}");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
