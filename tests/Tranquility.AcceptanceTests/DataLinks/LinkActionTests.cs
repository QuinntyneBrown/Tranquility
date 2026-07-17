using System.Net;
using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.DataLinks;

/// <summary>
/// L2-LNK-003: GIVEN a link with an advertised action WHEN run-action is
/// invoked THEN action result is returned according to method contract.
/// </summary>
[Requirement("L2-LNK-003")]
public sealed class LinkActionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Advertised_action_executes_and_returns_its_contracted_result()
    {
        using var admin = await fixture.AdminClientAsync();

        var link = await LinkApi.GetLinkAsync(admin, "tm-in");
        var action = link.GetProperty("actions").EnumerateArray()
            .Single(a => a.GetProperty("id").GetString() == "sendTestPacket");
        Assert.False(string.IsNullOrWhiteSpace(action.GetProperty("label").GetString()));
        Assert.True(action.GetProperty("enabled").GetBoolean());

        var before = await LinkApi.DataInCountAsync(admin);
        var response = await admin.PostAsync(
            $"/api/links/{TestConfig.Instance}/tm-in/actions/sendTestPacket", null);

        Assert.True(response.IsSuccessStatusCode, $"run-action returned {(int)response.StatusCode}");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("packetsInjected").GetInt32());

        await Eventually.Async(async () => await LinkApi.DataInCountAsync(admin) > before,
            "injected test packet is counted as link traffic");
    }

    [Fact]
    public async Task Unknown_action_returns_404_envelope()
    {
        using var admin = await fixture.AdminClientAsync();
        var response = await admin.PostAsync(
            $"/api/links/{TestConfig.Instance}/tm-in/actions/noSuchAction", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var (type, msg) = await JsonApiAssert.IsErrorEnvelopeAsync(response);
        Assert.Equal("NotFoundException", type);
        Assert.Contains("noSuchAction", msg, StringComparison.Ordinal);
    }
}
