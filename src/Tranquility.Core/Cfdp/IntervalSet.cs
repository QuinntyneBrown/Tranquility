namespace Tranquility.Core.Cfdp;

/// <summary>A half-open byte range [Start, End).</summary>
public readonly record struct ByteRange(long Start, long End)
{
    public long Length => End - Start;
}

/// <summary>
/// Sorted disjoint set of received byte ranges. Drives received-data tracking
/// and NAK segment-request generation regardless of arrival order (L2-FDP CFDP
/// reliability). Deterministic and pure.
/// </summary>
public sealed class IntervalSet
{
    private readonly List<ByteRange> _ranges = [];

    public IReadOnlyList<ByteRange> Ranges => _ranges;

    public bool IsEmpty => _ranges.Count == 0;

    /// <summary>True when [0, size) is fully covered.</summary>
    public bool IsComplete(long size) =>
        size == 0 || (_ranges.Count == 1 && _ranges[0].Start == 0 && _ranges[0].End >= size);

    public void Add(long start, long length)
    {
        if (length <= 0)
        {
            return;
        }

        var incoming = new ByteRange(start, start + length);
        var merged = new List<ByteRange>();
        bool placed = false;
        foreach (var range in _ranges)
        {
            if (range.End < incoming.Start)
            {
                merged.Add(range);
            }
            else if (range.Start > incoming.End)
            {
                if (!placed)
                {
                    merged.Add(incoming);
                    placed = true;
                }

                merged.Add(range);
            }
            else
            {
                // Overlaps or is adjacent: absorb into the incoming range.
                incoming = new ByteRange(Math.Min(incoming.Start, range.Start), Math.Max(incoming.End, range.End));
            }
        }

        if (!placed)
        {
            merged.Add(incoming);
        }

        _ranges.Clear();
        _ranges.AddRange(merged);
    }

    /// <summary>The gaps within [0, size) not yet received (NAK requests).</summary>
    public IReadOnlyList<ByteRange> Gaps(long size)
    {
        var gaps = new List<ByteRange>();
        long cursor = 0;
        foreach (var range in _ranges)
        {
            if (range.Start > cursor)
            {
                gaps.Add(new ByteRange(cursor, Math.Min(range.Start, size)));
            }

            cursor = Math.Max(cursor, range.End);
            if (cursor >= size)
            {
                break;
            }
        }

        if (cursor < size)
        {
            gaps.Add(new ByteRange(cursor, size));
        }

        return gaps;
    }
}
