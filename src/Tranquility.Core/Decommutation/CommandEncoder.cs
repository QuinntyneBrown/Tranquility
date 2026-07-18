using Tranquility.Core.Mdb;

namespace Tranquility.Core.Decommutation;

/// <summary>A resolved argument assignment in an encoded command.</summary>
public sealed record CommandAssignment(string Name, string Value);

/// <summary>The binary and assignments produced by encoding a MetaCommand.</summary>
public sealed record EncodedCommand(byte[] Binary, IReadOnlyList<CommandAssignment> Assignments);

/// <summary>
/// Encodes a MetaCommand into its binary form using the same big-endian
/// bit-writer discipline as decommutation, reversed. Implements the encoding
/// path for L2-CMD-001 (generated binary in the issue response).
/// </summary>
public static class CommandEncoder
{
    public static EncodedCommand Encode(MetaCommand command, IReadOnlyDictionary<string, string> userArgs)
    {
        var assignments = new List<CommandAssignment>();
        var bits = new List<bool>();

        foreach (var entry in command.Entries)
        {
            switch (entry)
            {
                case FixedValueEntry fixedValue:
                    AppendBits(bits, fixedValue.Value, fixedValue.SizeInBits);
                    break;

                case ArgumentRefEntry argumentRef:
                {
                    var argument = command.Arguments.FirstOrDefault(a => a.Name == argumentRef.ArgumentName)
                        ?? throw new ArgumentException(
                            $"Command '{command.QualifiedName}' references unknown argument '{argumentRef.ArgumentName}'.");
                    if (!userArgs.TryGetValue(argument.Name, out var userValue))
                    {
                        throw new MissingCommandArgumentException(argument.Name);
                    }

                    long raw = argument.Type.ToRaw(userValue);
                    AppendInteger(bits, raw, argument.Type.Encoding.SizeInBits);
                    assignments.Add(new CommandAssignment(argument.Name, userValue));
                    break;
                }
            }
        }

        return new EncodedCommand(PackBits(bits), assignments);
    }

    private static void AppendBits(List<bool> bits, byte[] value, int sizeInBits)
    {
        // Take the low `sizeInBits` bits of the big-endian value.
        int totalBits = value.Length * 8;
        for (int i = totalBits - sizeInBits; i < totalBits; i++)
        {
            int byteIndex = i >> 3;
            int bitInByte = 7 - (i & 7);
            bits.Add(((value[byteIndex] >> bitInByte) & 1) != 0);
        }
    }

    private static void AppendInteger(List<bool> bits, long value, int sizeInBits)
    {
        for (int i = sizeInBits - 1; i >= 0; i--)
        {
            bits.Add(((value >> i) & 1) != 0);
        }
    }

    private static byte[] PackBits(List<bool> bits)
    {
        var bytes = new byte[(bits.Count + 7) / 8];
        for (int i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                bytes[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            }
        }

        return bytes;
    }
}

/// <summary>A required command argument was not supplied (mapped to HTTP 400).</summary>
public sealed class MissingCommandArgumentException(string argumentName)
    : Exception($"Required command argument '{argumentName}' was not supplied")
{
    public string ArgumentName { get; } = argumentName;
}
