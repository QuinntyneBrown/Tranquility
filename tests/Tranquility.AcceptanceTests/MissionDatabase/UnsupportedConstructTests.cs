using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.MissionDatabase;

/// <summary>
/// L2-MDB-004: GIVEN an XTCE construct outside approved support WHEN load is
/// requested THEN response identifies the unsupported construct and location.
/// </summary>
[Requirement("L2-MDB-004")]
public sealed class UnsupportedConstructTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Unsupported_construct_is_identified_with_name_and_location()
    {
        using var admin = await fixture.AdminClientAsync();
        var response = await admin.PostAsJsonAsync(
            $"/api/mdb/{TestConfig.Instance}/load", new { xtceRef = "Unsupported.xml" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var report = doc.RootElement.GetProperty("validationReport").EnumerateArray().ToList();
        var finding = report.FirstOrDefault(e =>
            e.TryGetProperty("construct", out var c) && c.GetString() == "ArrayParameterType");
        Assert.True(finding.ValueKind == JsonValueKind.Object,
            $"Expected a finding naming construct 'ArrayParameterType': {body}");
        Assert.True(finding.GetProperty("line").GetInt32() > 0,
            "The finding must carry the construct's document location.");
    }
}
