using Tranquility.Application.Abstractions;
using Tranquility.Application.Processing;
using Tranquility.Core.Alarms;
using Tranquility.Infrastructure.Xtce;

namespace Tranquility.Application.Tests;

/// <summary>
/// Verifies the link-to-subscription pipeline (L2-PAR-001/002, L2-RTS-002,
/// L2-LIF-002) deterministically with a fixed clock.
/// </summary>
public class TelemetryProcessorTests
{
    /// <summary>APID 100 science packet: counter 258, temp raw 1024, mode 2, volts 1.5f.</summary>
    private static readonly byte[] GoldenPacket =
    [
        0x00, 0x64, 0xC0, 0x05, 0x00, 0x07,
        0x01, 0x02,
        0x40, 0x02,
        0x3F, 0xC0, 0x00, 0x00,
    ];

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private static TelemetryProcessor CreateProcessor(
        out ParameterCache cache, out SubscriptionManager subscriptions, out AlarmStateTracker alarms)
    {
        var mdb = new XtceLoader(Path.Combine(AppContext.BaseDirectory, "SampleSat.xml")).Load();
        cache = new ParameterCache();
        subscriptions = new SubscriptionManager();
        alarms = new AlarmStateTracker();
        return new TelemetryProcessor(
            "sample", "realtime", mdb, mdb.FindContainer("/SampleSat/Root")!,
            links: [], cache, subscriptions, alarms, new FixedClock());
    }

    [Fact]
    public void ProcessPacket_UpdatesCacheWithCalibratedValues()
    {
        var processor = CreateProcessor(out var cache, out _, out _);

        processor.ProcessPacket(GoldenPacket);

        Assert.True(cache.TryGet("/SampleSat/Temperature", out var temp));
        Assert.Equal(31.2, Assert.IsType<double>(temp.EngValue), precision: 10);
        Assert.Equal(MonitoringResult.Warning, temp.Monitoring);
        Assert.True(cache.TryGet("/SampleSat/Mode", out var mode));
        Assert.Equal("SCIENCE", mode.EngValue);
        Assert.Equal(1, processor.PacketCount);
    }

    [Fact]
    public void ProcessPacket_StampsFixedClockTimes()
    {
        var processor = CreateProcessor(out var cache, out _, out _);
        processor.ProcessPacket(GoldenPacket);

        cache.TryGet("/SampleSat/Counter", out var counter);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero), counter.GenerationTime);
    }

    [Fact]
    public void ProcessPacket_RaisesAlarmForWarningViolation()
    {
        var processor = CreateProcessor(out _, out _, out var alarms);
        processor.ProcessPacket(GoldenPacket);

        var alarm = Assert.Single(alarms.ActiveAlarms);
        Assert.Equal("/SampleSat/Temperature", alarm.Parameter.QualifiedName);
        Assert.Equal(MonitoringResult.Warning, alarm.Severity);
    }

    [Fact]
    public async Task Subscription_ReceivesOnlyRequestedParameters()
    {
        var processor = CreateProcessor(out _, out var subscriptions, out _);
        using var subscription = subscriptions.Subscribe(["/SampleSat/BusVoltage"]);

        processor.ProcessPacket(GoldenPacket);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var batch = await subscription.Reader.ReadAsync(cts.Token);
        var value = Assert.Single(batch);
        Assert.Equal("/SampleSat/BusVoltage", value.Parameter.QualifiedName);
        Assert.Equal(1.5, Assert.IsType<double>(value.EngValue));
    }

    [Fact]
    public void Subscription_Disposed_StopsReceiving()
    {
        var processor = CreateProcessor(out _, out var subscriptions, out _);
        var subscription = subscriptions.Subscribe();
        subscription.Dispose();

        processor.ProcessPacket(GoldenPacket);

        Assert.Equal(0, subscriptions.ActiveCount);
        Assert.False(subscription.Reader.TryRead(out _));
    }
}
