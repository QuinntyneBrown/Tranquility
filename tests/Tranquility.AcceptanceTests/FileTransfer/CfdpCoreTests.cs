using CsCheck;
using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Core.Cfdp;
using Xunit;

namespace Tranquility.AcceptanceTests.FileTransfer;

/// <summary>
/// Deterministic Core behaviour behind the CFDP transfer requirements: PDU
/// round-trip, interval convergence under reordering, checksum, and the
/// sender/receiver engines completing a transfer through a lossless glue.
/// </summary>
public sealed class CfdpCoreTests
{
    [Fact]
    [Requirement("L2-FDP-001")]
    public void Pdu_round_trips_through_the_codec()
    {
        Pdu[] pdus =
        [
            new MetadataPdu(7, "buckets/in/a.bin", "incoming/a.bin", 2048, Acknowledged: true),
            new FileDataPdu(7, 1024, [1, 2, 3, 4, 5]),
            new EofPdu(7, ConditionCode.NoError, 0xDEADBEEF, 2048),
            new FinishedPdu(7, ConditionCode.NoError, FinishedDeliveryCode.Complete),
            new AckPdu(7, PduType.Eof),
            new NakPdu(7, [new ByteRange(0, 1024), new ByteRange(1536, 2048)]),
        ];

        foreach (var pdu in pdus)
        {
            var decoded = PduCodec.Decode(PduCodec.Encode(pdu));
            Assert.Equal(pdu, decoded);
        }
    }

    [Fact]
    [Requirement("L2-FDP-001")]
    public void Interval_set_converges_regardless_of_segment_order()
    {
        Gen.Int[0, 20].Array[8].Sample(order =>
        {
            var set = new IntervalSet();
            // Eight 100-byte segments covering [0, 800) added in a shuffled order.
            foreach (var i in order.Select(o => o % 8).Distinct())
            {
                set.Add(i * 100, 100);
            }

            var covered = order.Select(o => o % 8).Distinct().Count();
            return set.Ranges.Sum(r => r.Length) == covered * 100;
        }, iter: 200);
    }

    [Fact]
    [Requirement("L2-FDP-001")]
    public void Interval_set_reports_gaps_for_missing_segments()
    {
        var set = new IntervalSet();
        set.Add(0, 100);
        set.Add(300, 100); // gap [100,300) and [400,500)
        var gaps = set.Gaps(500);

        Assert.Equal(2, gaps.Count);
        Assert.Equal(new ByteRange(100, 300), gaps[0]);
        Assert.Equal(new ByteRange(400, 500), gaps[1]);
        Assert.False(set.IsComplete(500));

        set.Add(100, 200);
        set.Add(400, 100);
        Assert.True(set.IsComplete(500));
    }

    [Theory]
    [InlineData(1)]  // drop every FileData: forces class-2 NAK retransmission
    [InlineData(3)]
    [InlineData(0)]  // lossless
    [Requirement("L2-FDP-001")]
    public void Acknowledged_transfer_delivers_the_file_through_a_lossy_channel(int dropEveryNth)
    {
        var content = new byte[4096];
        new Random(42).NextBytes(content);
        var harness = new CfdpLoopbackHarness(content, CfdpClass.Acknowledged, dropEveryNth);

        harness.RunToCompletion();

        Assert.Equal(CfdpTransactionState.Complete, harness.Sender.State);
        Assert.Equal(content, harness.DeliveredFile);
    }

    [Fact]
    [Requirement("L2-FDP-001")]
    public void Unacknowledged_transfer_delivers_the_file_when_lossless()
    {
        var content = new byte[2048];
        new Random(7).NextBytes(content);
        var harness = new CfdpLoopbackHarness(content, CfdpClass.Unacknowledged, dropEveryNth: 0);

        harness.RunToCompletion();

        Assert.Equal(content, harness.DeliveredFile);
    }
}
