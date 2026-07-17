using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.MissionDatabase;

/// <summary>
/// L2-MDB-001: GIVEN an XTCE document with broken references WHEN load is
/// requested THEN activation is rejected with a validation report — an
/// exhaustive one (every broken reference, not fail-fast), and any previously
/// active mission database stays active.
/// </summary>
[Requirement("L2-MDB-001")]
public sealed class XtceValidationTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Valid_document_loads_and_activates_at_startup()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/mdb/{TestConfig.Instance}");

        Assert.True(response.IsSuccessStatusCode,
            $"GET /api/mdb returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("parameterCount").GetInt32() > 0);
    }

    [Fact]
    public async Task Broken_references_reject_activation_with_an_exhaustive_report()
    {
        using var admin = await fixture.AdminClientAsync();

        var before = await ParameterCountAsync(admin);
        var response = await admin.PostAsJsonAsync(
            $"/api/mdb/{TestConfig.Instance}/load", new { xtceRef = "BrokenRefs.xml" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("exception", out var exception),
            $"422 body lacks the exception envelope: {body}");
        Assert.Equal("ValidationException", exception.GetProperty("type").GetString());

        Assert.True(doc.RootElement.TryGetProperty("validationReport", out var report),
            $"422 body lacks validationReport: {body}");
        var errors = report.EnumerateArray().ToList();
        Assert.True(errors.Count >= 3,
            $"Expected all three broken references reported, got {errors.Count}: {body}");
        foreach (var error in errors)
        {
            Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
            Assert.True(error.GetProperty("line").GetInt32() > 0, "Each finding carries its document line.");
        }

        var messages = string.Join("\n", errors.Select(e => e.GetProperty("message").GetString()));
        Assert.Contains("missingType", messages, StringComparison.Ordinal);
        Assert.Contains("missingParameter", messages, StringComparison.Ordinal);
        Assert.Contains("missingContainer", messages, StringComparison.Ordinal);

        // The previously active model is untouched.
        Assert.Equal(before, await ParameterCountAsync(admin));
    }

    private static async Task<int> ParameterCountAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/mdb/{TestConfig.Instance}"));
        return doc.RootElement.GetProperty("parameterCount").GetInt32();
    }
}
