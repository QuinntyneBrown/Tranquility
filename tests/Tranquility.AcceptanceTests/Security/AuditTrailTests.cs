using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Commanding;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Security;

/// <summary>
/// L2-SEC-004: GIVEN successful or denied privileged operations WHEN audit is
/// queried THEN corresponding records include actor, action, timestamp, and
/// outcome — in an append-only, integrity-verifiable store.
/// </summary>
[Requirement("L2-SEC-004")]
public sealed class AuditTrailTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Audit_records_authentication_denials_and_uplink_actions_with_required_fields()
    {
        using var admin = await fixture.AdminClientAsync();
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);

        // Produce: auth success (token issue), authorization denial, uplink action.
        var denial = await observer.PostAsync($"/api/links/{TestConfig.Instance}/tm-in:disable", null);
        Assert.Equal(HttpStatusCode.Forbidden, denial.StatusCode);
        var commandId = await CommandIssueTests.IssueAsync(admin);
        await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{commandId}:accept", null);

        await Eventually.Async(async () =>
        {
            var records = await RecordsAsync(admin);
            return HasRecord(records, TestConfig.AdminUser, "token-issue", "success")
                && HasRecord(records, TestConfig.ObserverUser, "authz-denied", "denied")
                && HasRecord(records, TestConfig.AdminUser, "command-issue", "success")
                && HasRecord(records, TestConfig.AdminUser, "command-release", "success");
        }, "audit contains authentication, denial, and uplink records");

        foreach (var record in await RecordsAsync(admin))
        {
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("actor").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("action").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("outcome").GetString()));
            Assert.Matches(JsonApiAssert.Rfc3339Utc(), record.GetProperty("timestamp").GetString()!);
        }
    }

    [Fact]
    public async Task Audit_chain_verifies_and_reading_it_requires_the_read_audit_privilege()
    {
        using var admin = await fixture.AdminClientAsync();
        await CommandIssueTests.IssueAsync(admin); // ensure at least one record

        var verify = await admin.PostAsync("/api/audit:verify", null);
        Assert.True(verify.IsSuccessStatusCode, $":verify returned {(int)verify.StatusCode}");
        using var doc = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("count").GetInt64() > 0);

        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);
        var denied = await observer.GetAsync("/api/audit/records");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static bool HasRecord(List<JsonElement> records, string actor, string action, string outcome) =>
        records.Any(r => r.GetProperty("actor").GetString() == actor
            && r.GetProperty("action").GetString() == action
            && r.GetProperty("outcome").GetString() == outcome);

    private static async Task<List<JsonElement>> RecordsAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/audit/records"));
        return doc.RootElement.GetProperty("records").EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
