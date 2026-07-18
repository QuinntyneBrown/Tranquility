using System.Buffers.Binary;

namespace Tranquility.Core.Cfdp;

/// <summary>
/// CCSDS 727.0-B modular checksum (type 0): the 32-bit modular sum of the file
/// contents aligned on 4-octet boundaries from the start of file.
/// </summary>
public static class ModularChecksum
{
    public static uint Compute(ReadOnlySpan<byte> file)
    {
        uint sum = 0;
        Span<byte> word = stackalloc byte[4];
        for (int offset = 0; offset < file.Length; offset += 4)
        {
            word.Clear();
            int take = Math.Min(4, file.Length - offset);
            file.Slice(offset, take).CopyTo(word);
            sum += BinaryPrimitives.ReadUInt32BigEndian(word);
        }

        return sum;
    }

    /// <summary>Accumulates a segment placed at <paramref name="fileOffset"/> into a running sum.</summary>
    public static uint Accumulate(uint sum, long fileOffset, ReadOnlySpan<byte> segment)
    {
        // Realign the segment onto the file's 4-octet grid.
        long position = fileOffset;
        int index = 0;
        Span<byte> word = stackalloc byte[4];
        while (index < segment.Length)
        {
            int lane = (int)(position % 4);
            word.Clear();
            int take = Math.Min(4 - lane, segment.Length - index);
            segment.Slice(index, take).CopyTo(word[lane..]);
            sum += BinaryPrimitives.ReadUInt32BigEndian(word);
            position += take;
            index += take;
        }

        return sum;
    }
}
