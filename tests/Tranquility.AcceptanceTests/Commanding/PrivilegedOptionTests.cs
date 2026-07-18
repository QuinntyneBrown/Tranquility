using System.Net;
using System.Net.Http.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-006: GIVEN a caller without elevated privilege WHEN privileged
/// command options are submitted THEN the request is denied with
/// authorization error.
/// </summary>
[Requirement("L2-CMD-006")]
public sealed class PrivilegedOptionTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Theory]
    [InlineData("disableVerifiers")]
    [InlineData("disableTransmissionConstraints")]
    public async Task Privileged_options_require_command_override(string option)
    {
        // The operator holds CommandIssue but not CommandOverride.
        using var operatorClient = await fixture.AuthenticatedClientAsync(
            TestConfig.OperatorUser, TestConfig.OperatorPassword);
        var payload = new Dictionary<string, object?>
        {
            ["args"] = new { mode = "SCIENCE" },
            [option] = true,
        };
        var denied = await operatorClient.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode", payload);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var (type, _) = await JsonApiAssert.IsErrorEnvelopeAsync(denied);
        Assert.Equal("ForbiddenException", type);

        // A superuser may use the same option.
        using var admin = await fixture.AdminClientAsync();
        var allowed = await admin.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode", payload);
        Assert.True(allowed.IsSuccessStatusCode,
            $"superuser with {option} got {(int)allowed.StatusCode}: {await allowed.Content.ReadAsStringAsync()}");
    }
}
