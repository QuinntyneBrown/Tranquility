using System.Buffers.Binary;
using System.Text;

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

public sealed record FileDataPdu(long TransactionId, long Offset, byte[] Data) : Pdu(TransactionId)
{
    public bool Equals(FileDataPdu? other) =>
        other is not null && TransactionId == other.TransactionId && Offset == other.Offset
        && Data.AsSpan().SequenceEqual(other.Data);

    public override int GetHashCode() => HashCode.Combine(TransactionId, Offset, Data.Length);
}

public sealed record EofPdu(long TransactionId, ConditionCode Condition, uint Checksum, long FileSize) : Pdu(TransactionId);

public sealed record FinishedPdu(long TransactionId, ConditionCode Condition, FinishedDeliveryCode Delivery) : Pdu(TransactionId);

public sealed record AckPdu(long TransactionId, PduType AcknowledgedType) : Pdu(TransactionId);

public sealed record NakPdu(long TransactionId, IReadOnlyList<ByteRange> Gaps) : Pdu(TransactionId)
{
    public bool Equals(NakPdu? other) =>
        other is not null && TransactionId == other.TransactionId && Gaps.SequenceEqual(other.Gaps);

    public override int GetHashCode() => HashCode.Combine(TransactionId, Gaps.Count);
}

/// <summary>
/// Encodes/decodes the baseline CFDP PDU set (TRQ-CFDP-BP1). Compact
/// big-endian framing sufficient for the baseline profile's PDU semantics:
/// [type:1][transactionId:8][type-specific fields].
/// </summary>
public static class PduCodec
{
    public static byte[] Encode(Pdu pdu)
    {
        using var stream = new MemoryStream();

        void WriteLong(long value)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            stream.Write(buffer);
        }

        void WriteUInt(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteUInt((uint)bytes.Length);
            stream.Write(bytes);
        }

        stream.WriteByte((byte)TypeOf(pdu));
        WriteLong(pdu.TransactionId);

        switch (pdu)
        {
            case MetadataPdu m:
                stream.WriteByte((byte)(m.Acknowledged ? 1 : 0));
                WriteLong(m.FileSize);
                WriteString(m.SourceName);
                WriteString(m.DestName);
                break;
            case FileDataPdu f:
                WriteLong(f.Offset);
                WriteUInt((uint)f.Data.Length);
                stream.Write(f.Data);
                break;
            case EofPdu e:
                stream.WriteByte((byte)e.Condition);
                WriteUInt(e.Checksum);
                WriteLong(e.FileSize);
                break;
            case FinishedPdu fin:
                stream.WriteByte((byte)fin.Condition);
                stream.WriteByte((byte)fin.Delivery);
                break;
            case AckPdu a:
                stream.WriteByte((byte)a.AcknowledgedType);
                break;
            case NakPdu n:
                WriteUInt((uint)n.Gaps.Count);
                foreach (var gap in n.Gaps)
                {
                    WriteLong(gap.Start);
                    WriteLong(gap.End);
                }

                break;
        }

        return stream.ToArray();
    }

    public static Pdu Decode(ReadOnlySpan<byte> bytes)
    {
        var type = (PduType)bytes[0];
        int pos = 1;
        long transactionId = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
        pos += 8;

        switch (type)
        {
            case PduType.Metadata:
            {
                bool ack = bytes[pos++] != 0;
                long size = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
                pos += 8;
                string source = ReadString(bytes, ref pos);
                string dest = ReadString(bytes, ref pos);
                return new MetadataPdu(transactionId, source, dest, size, ack);
            }

            case PduType.FileData:
            {
                long offset = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
                pos += 8;
                int len = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[pos..]);
                pos += 4;
                return new FileDataPdu(transactionId, offset, bytes.Slice(pos, len).ToArray());
            }

            case PduType.Eof:
            {
                var condition = (ConditionCode)bytes[pos++];
                uint checksum = BinaryPrimitives.ReadUInt32BigEndian(bytes[pos..]);
                pos += 4;
                long size = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
                return new EofPdu(transactionId, condition, checksum, size);
            }

            case PduType.Finished:
            {
                var condition = (ConditionCode)bytes[pos++];
                var delivery = (FinishedDeliveryCode)bytes[pos];
                return new FinishedPdu(transactionId, condition, delivery);
            }

            case PduType.Ack:
                return new AckPdu(transactionId, (PduType)bytes[pos]);

            default:
            {
                int count = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[pos..]);
                pos += 4;
                var gaps = new List<ByteRange>(count);
                for (int i = 0; i < count; i++)
                {
                    long start = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
                    pos += 8;
                    long end = BinaryPrimitives.ReadInt64BigEndian(bytes[pos..]);
                    pos += 8;
                    gaps.Add(new ByteRange(start, end));
                }

                return new NakPdu(transactionId, gaps);
            }
        }
    }

    private static string ReadString(ReadOnlySpan<byte> bytes, ref int pos)
    {
        int len = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[pos..]);
        pos += 4;
        var value = Encoding.UTF8.GetString(bytes.Slice(pos, len));
        pos += len;
        return value;
    }

    private static PduType TypeOf(Pdu pdu) => pdu switch
    {
        MetadataPdu => PduType.Metadata,
        FileDataPdu => PduType.FileData,
        EofPdu => PduType.Eof,
        FinishedPdu => PduType.Finished,
        AckPdu => PduType.Ack,
        NakPdu => PduType.Nak,
        _ => throw new ArgumentException($"Unknown PDU type {pdu.GetType().Name}"),
    };
}
