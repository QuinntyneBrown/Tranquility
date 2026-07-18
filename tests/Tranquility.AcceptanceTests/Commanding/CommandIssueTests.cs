using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-001 / L2-API-002: GIVEN a valid command payload WHEN posted to the
/// documented Issue Command URI THEN the response includes command identity,
/// assignment fields, and the generated binary.
/// </summary>
public sealed class CommandIssueTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    /// <summary>FixedValueEntry 1064C0000000 + mode SCIENCE (0x02).</summary>
    internal static readonly byte[] ExpectedBinary = [0x10, 0x64, 0xC0, 0x00, 0x00, 0x00, 0x02];

    [Fact]
    [Requirement("L2-CMD-001")]
    [Requirement("L2-API-002")]
    public async Task Issue_returns_command_identity_assignments_and_generated_binary()
    {
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);

        var response = await operatorClient.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { mode = "SCIENCE" } });

        Assert.True(response.IsSuccessStatusCode,
            $"issue returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.Equal("/SampleSat/SwitchMode", root.GetProperty("commandName").GetString());
        Assert.Matches(JsonApiAssert.Rfc3339Utc(), root.GetProperty("generationTime").GetString()!);
        Assert.True(root.GetProperty("sequenceNumber").GetInt32() >= 0);
        Assert.Equal(TestConfig.OperatorUser, root.GetProperty("origin").GetString());

        var assignment = root.GetProperty("assignments").EnumerateArray()
            .Single(a => a.GetProperty("name").GetString() == "mode");
        Assert.Equal("SCIENCE", assignment.GetProperty("value").GetString());

        Assert.Equal(Convert.ToBase64String(ExpectedBinary), root.GetProperty("binary").GetString());
    }

    [Fact]
    [Requirement("L2-CMD-001")]
    public async Task Missing_required_argument_is_a_400_envelope()
    {
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);
        var response = await operatorClient.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (_, msg) = await JsonApiAssert.IsErrorEnvelopeAsync(response);
        Assert.Contains("mode", msg, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("L2-CMD-001")]
    public async Task Unknown_command_is_a_404_envelope()
    {
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);
        var response = await operatorClient.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/NoSuchCommand",
            new { args = new { } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await JsonApiAssert.IsErrorEnvelopeAsync(response);
    }

    internal static async Task<string> IssueAsync(HttpClient client, object? args = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = args ?? new { mode = "SCIENCE" } });
        Assert.True(response.IsSuccessStatusCode,
            $"issue returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }
}
