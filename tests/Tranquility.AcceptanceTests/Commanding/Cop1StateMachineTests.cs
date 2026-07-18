using System.Text.Json;
using Tranquility.AcceptanceTests.Fixtures;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Cop1;
using Xunit;

namespace Tranquility.AcceptanceTests.Commanding;

/// <summary>
/// L2-CMD-003: GIVEN a COP-1 session configured to mission defaults WHEN
/// command transfer executes THEN observed protocol state transitions follow
/// the selected profile. Golden state-transition scripts drive the pure FOP-1
/// engine; the API half observes a live transfer over the loopback TC link.
/// </summary>
[Requirement("L2-CMD-003")]
public sealed class Cop1StateMachineTests(InProcApiFixture fixture) : IClassFixture<InProcApiFixture>
{
    private static readonly byte[] Frame = [0xAA, 0xBB, 0xCC];

    [Fact]
    public void Nominal_transfer_radiates_starts_timer_and_acknowledges_on_clean_clcw()
    {
        var engine = new FopEngine(new Cop1Profile(TransmissionLimit: 3, T1TimeoutMs: 1000));
        Assert.Equal(FopState.Active, engine.State);
        Assert.Equal(0, engine.Vs);

        var outputs = engine.Handle(new TransmitAdRequest(1, Frame));
        var radiate = Assert.Single(outputs.OfType<RadiateFrame>());
        Assert.Equal(0, radiate.FrameSequenceNumber);
        Assert.Single(outputs.OfType<StartTimer>());
        Assert.Equal(1, engine.Vs);
        Assert.Equal(1, engine.SentQueueDepth);

        outputs = engine.Handle(new ClcwReceived(new Clcw(ReportValue: 1, false, false, false)));
        Assert.Equal(1, Assert.Single(outputs.OfType<PositiveConfirm>()).RequestId);
        Assert.Single(outputs.OfType<CancelTimer>());
        Assert.Equal(1, engine.NnR);
        Assert.Equal(0, engine.SentQueueDepth);
        Assert.Equal(FopState.Active, engine.State);
    }

    [Fact]
    public void Retransmit_flag_re_radiates_unacknowledged_frames()
    {
        var engine = new FopEngine(new Cop1Profile());
        engine.Handle(new TransmitAdRequest(1, Frame));

        var outputs = engine.Handle(new ClcwReceived(new Clcw(ReportValue: 0, RetransmitFlag: true, false, false)));
        Assert.Equal(FopState.RetransmitWithoutWait, engine.State);
        Assert.Equal(0, Assert.Single(outputs.OfType<RadiateFrame>()).FrameSequenceNumber);

        outputs = engine.Handle(new ClcwReceived(new Clcw(ReportValue: 1, false, false, false)));
        Assert.Single(outputs.OfType<PositiveConfirm>());
        Assert.Equal(FopState.Active, engine.State);
    }

    [Fact]
    public void Timer_expiry_retransmits_until_the_limit_then_alerts_into_initial_state()
    {
        var engine = new FopEngine(new Cop1Profile(TransmissionLimit: 2, T1TimeoutMs: 500));
        engine.Handle(new TransmitAdRequest(7, Frame));
        Assert.Equal(1, engine.TransmissionCount);

        // First expiry: within the limit, retransmit and restart the timer.
        var outputs = engine.Handle(new TimerExpired());
        Assert.Single(outputs.OfType<RadiateFrame>());
        Assert.Single(outputs.OfType<StartTimer>());
        Assert.Equal(2, engine.TransmissionCount);

        // Second expiry: limit exceeded — alert, purge, negative confirm.
        outputs = engine.Handle(new TimerExpired());
        Assert.Contains(outputs.OfType<Alert>(), a => a.Reason.Contains("T1", StringComparison.Ordinal));
        Assert.Equal(7, Assert.Single(outputs.OfType<NegativeConfirm>()).RequestId);
        Assert.Equal(FopState.Initial, engine.State);
        Assert.Equal(0, engine.SentQueueDepth);
    }

    [Fact]
    public void Lockout_flag_alerts_and_purges()
    {
        var engine = new FopEngine(new Cop1Profile());
        engine.Handle(new TransmitAdRequest(3, Frame));

        var outputs = engine.Handle(new ClcwReceived(new Clcw(0, false, false, LockoutFlag: true)));
        Assert.Contains(outputs.OfType<Alert>(), a => a.Reason.Contains("lockout", StringComparison.OrdinalIgnoreCase));
        Assert.Single(outputs.OfType<NegativeConfirm>());
        Assert.Equal(FopState.Initial, engine.State);
    }

    [Fact]
    public void Full_sliding_window_rejects_further_ad_requests_as_busy()
    {
        var engine = new FopEngine(new Cop1Profile(SlidingWindowK: 2));
        engine.Handle(new TransmitAdRequest(1, Frame));
        engine.Handle(new TransmitAdRequest(2, Frame));

        var outputs = engine.Handle(new TransmitAdRequest(3, Frame));
        Assert.Equal(3, Assert.Single(outputs.OfType<RejectBusy>()).RequestId);
        Assert.Equal(2, engine.SentQueueDepth);
    }

    [Fact]
    public async Task Accepted_command_transfers_through_cop1_and_drains_on_loopback_ack()
    {
        using var admin = await fixture.AdminClientAsync();

        using (var doc = JsonDocument.Parse(await admin.GetStringAsync(
            $"/api/cop1/{TestConfig.Instance}/tc-out/status")))
        {
            Assert.Equal("ACTIVE", doc.RootElement.GetProperty("state").GetString());
        }

        var id = await CommandIssueTests.IssueAsync(admin);
        var accept = await admin.PostAsync(
            $"/api/processors/{TestConfig.Instance}/realtime/queues/default/entries/{id}:accept", null);
        Assert.True(accept.IsSuccessStatusCode, $":accept returned {(int)accept.StatusCode}");

        // Loopback spacecraft acknowledges: V(S) advances, sent queue drains.
        await Eventually.Async(async () =>
        {
            using var doc = JsonDocument.Parse(await admin.GetStringAsync(
                $"/api/cop1/{TestConfig.Instance}/tc-out/status"));
            var root = doc.RootElement;
            return root.GetProperty("vS").GetInt32() >= 1
                && root.GetProperty("sentQueueDepth").GetInt32() == 0
                && root.GetProperty("state").GetString() == "ACTIVE";
        }, "COP-1 transfer completes against the loopback spacecraft");
    }
}
