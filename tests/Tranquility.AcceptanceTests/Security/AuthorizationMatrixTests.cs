using System.Net;
using System.Net.Http.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Xunit;

namespace Tranquility.AcceptanceTests.Security;

/// <summary>
/// L2-SEC-003: GIVEN an authenticated user lacking required privilege WHEN
/// privileged control methods are called THEN requests are denied with
/// authorization error — across command issue, queue action, and link
/// control.
/// </summary>
[Requirement("L2-SEC-003")]
public sealed class AuthorizationMatrixTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    [Fact]
    public async Task Observer_is_denied_command_issue_queue_action_and_link_control()
    {
        using var observer = await fixture.AuthenticatedClientAsync(
            TestConfig.ObserverUser, TestConfig.ObserverPassword);

        var issue = await observer.PostAsJsonAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/commands/SampleSat/SwitchMode",
            new { args = new { mode = "SAFE" } });
        Assert.Equal(HttpStatusCode.Forbidden, issue.StatusCode);
        Assert.Equal("ForbiddenException", (await JsonApiAssert.IsErrorEnvelopeAsync(issue)).Type);

        var queue = await observer.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/any:accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);
        Assert.Equal("ForbiddenException", (await JsonApiAssert.IsErrorEnvelopeAsync(queue)).Type);

        var link = await observer.PostAsync(
            $"/api/links/{TestConfig.Instance}/tm-in:disable", null);
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
        Assert.Equal("ForbiddenException", (await JsonApiAssert.IsErrorEnvelopeAsync(link)).Type);
    }
}
