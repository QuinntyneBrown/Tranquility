namespace Tranquility.Core.Ccsds;

/// <summary>
/// Extracts space packets from a sequence of TM transfer frame data fields (M_PDU
/// first-header-pointer packet extraction, including packets spanning frame boundaries).
/// Implements: L2-SDL-002. Source: CCSDS 132.0-B (TM Space Data Link Protocol).
///
/// Deterministic state machine with no I/O dependencies (L2-QLT-002). One instance
/// per virtual channel; the caller is responsible for demultiplexing by VCID.
/// </summary>
public sealed class VirtualChannelPacketExtractor
{
    private readonly List<byte> _pending = new();
    private bool _pendingValid;

    /// <summary>Number of partial packets discarded due to inconsistent continuation data.</summary>
    public long DiscardedCount { get; private set; }

    /// <summary>
    /// Feeds one frame data field and returns the complete packets it closed or contained.
    /// </summary>
    /// <param name="dataField">The frame data field (after primary/secondary header, before OCF/FECF).</param>
    /// <param name="firstHeaderPointer">The first-header-pointer from the frame data field status.</param>
    public IReadOnlyList<byte[]> Feed(ReadOnlySpan<byte> dataField, ushort firstHeaderPointer)
    {
        var completed = new List<byte[]>();

        if (firstHeaderPointer == TmFrameHeader.FhpIdleData)
        {
            // Frame contains only idle data; pending partial-packet state is unaffected.
            return completed;
        }

        if (firstHeaderPointer == TmFrameHeader.FhpNoPacketStart)
        {
            if (_pendingValid)
            {
                AppendPending(dataField);
                TryCompletePending(completed);
            }

            return completed;
        }

        if (firstHeaderPointer > dataField.Length)
        {
            // Malformed pointer: drop any pending state.
            Reset();
            return completed;
        }

        // Continuation portion belonging to the packet started in an earlier frame.
        if (_pendingValid)
        {
            AppendPending(dataField[..firstHeaderPointer]);
            TryCompletePending(completed);
            if (_pendingValid)
            {
                // Pending packet still incomplete although a new packet starts here: inconsistent.
                Reset();
            }
        }

        // Packets starting in this frame.
        var remaining = dataField[firstHeaderPointer..];
        while (!remaining.IsEmpty)
        {
            if (remaining.Length < SpacePacketHeader.Length)
            {
                AppendPending(remaining);
                _pendingValid = true;
                break;
            }

            var header = SpacePacketHeader.Parse(remaining);
            int total = header.TotalPacketLength;
            if (remaining.Length < total)
            {
                AppendPending(remaining);
                _pendingValid = true;
                break;
            }

            completed.Add(remaining[..total].ToArray());
            remaining = remaining[total..];
        }

        return completed;
    }

    /// <summary>Discards accumulated partial-packet state (e.g., after frame sync loss).</summary>
    public void Reset()
    {
        if (_pendingValid)
        {
            DiscardedCount++;
        }

        _pending.Clear();
        _pendingValid = false;
    }

    private void AppendPending(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            _pending.Add(b);
        }
    }

    private void TryCompletePending(List<byte[]> completed)
    {
        while (_pendingValid && _pending.Count >= SpacePacketHeader.Length)
        {
            var header = SpacePacketHeader.Parse(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pending));
            int total = header.TotalPacketLength;
            if (_pending.Count < total)
            {
                return;
            }

            completed.Add(_pending.Take(total).ToArray());
            _pending.RemoveRange(0, total);
            if (_pending.Count == 0)
            {
                _pendingValid = false;
            }
        }
    }
}
