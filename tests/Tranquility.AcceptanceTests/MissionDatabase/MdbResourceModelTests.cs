using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.MissionDatabase;

/// <summary>
/// L2-MDB-002: GIVEN an active mission database WHEN MDB API methods are
/// called THEN model counts and hierarchy data are returned.
/// </summary>
[Requirement("L2-MDB-002")]
public sealed class MdbResourceModelTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Overview_returns_version_and_model_counts()
    {
        using var client = fixture.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/mdb/{TestConfig.Instance}"));
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));
        Assert.Equal(11, root.GetProperty("parameterCount").GetInt32());
        Assert.Equal(2, root.GetProperty("containerCount").GetInt32());
        Assert.True(root.GetProperty("parameterTypeCount").GetInt32() >= 9);
        Assert.Equal(0, root.GetProperty("commandCount").GetInt32());
    }

    [Fact]
    public async Task Space_system_hierarchy_is_returned_with_qualified_names()
    {
        using var client = fixture.CreateClient();
        using var doc = JsonDocument.Parse(
            await client.GetStringAsync($"/api/mdb/{TestConfig.Instance}/space-systems"));

        var systems = doc.RootElement.GetProperty("spaceSystems").EnumerateArray().ToList();
        var sampleSat = Assert.Single(systems);
        Assert.Equal("SampleSat", sampleSat.GetProperty("name").GetString());
        Assert.Equal("/SampleSat", sampleSat.GetProperty("qualifiedName").GetString());
        Assert.Equal(11, sampleSat.GetProperty("parameterCount").GetInt32());
    }
}
