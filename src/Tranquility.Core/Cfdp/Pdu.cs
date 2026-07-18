namespace Tranquility.Core.Cfdp;

public enum PduType
{
    Metadata = 0,
    FileData = 1,
    Eof = 2,
    Finished = 3,
    Ack = 4,
    Nak = 5,
}

/// <summary>CFDP condition codes used by EOF/Finished (subset in baseline scope).</summary>
public enum ConditionCode
{
    NoError = 0,
    FilestoreRejection = 1,
    ChecksumFailure = 2,
    CancelRequestReceived = 3,
    InactivityDetected = 4,
}

public enum FinishedDeliveryCode
{
    Complete = 0,
    Incomplete = 1,
}

public abstract record Pdu(long TransactionId);

public sealed record MetadataPdu(long TransactionId, string SourceName, string DestName, long FileSize, bool Acknowledged)
    : Pdu(TransactionId);

public sealed record FileDataPdu(long TransactionId, long Offset, byte[] Data) : Pdu(TransactionId);

public sealed record EofPdu(long TransactionId, ConditionCode Condition, uint Checksum, long FileSize) : Pdu(TransactionId);

public sealed record FinishedPdu(long TransactionId, ConditionCode Condition, FinishedDeliveryCode Delivery) : Pdu(TransactionId);

public sealed record AckPdu(long TransactionId, PduType AcknowledgedType) : Pdu(TransactionId);

public sealed record NakPdu(long TransactionId, IReadOnlyList<ByteRange> Gaps) : Pdu(TransactionId);

/// <summary>
/// Encodes/decodes the baseline CFDP PDU set (TRQ-CFDP-BP1). Compact,
/// deterministic framing sufficient for the baseline profile's PDU semantics.
/// </summary>
public static class PduCodec
{
    public static byte[] Encode(Pdu pdu)
    {
        throw new NotImplementedException();
    }

    public static Pdu Decode(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }
}
