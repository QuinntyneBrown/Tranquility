namespace Tranquility.Core.Mdb;

/// <summary>XTCE argument type (scalar types shared with parameter types).</summary>
public abstract class ArgumentType(string name, DataEncoding encoding)
{
    public string Name { get; } = name;

    public DataEncoding Encoding { get; } = encoding;

    /// <summary>Converts a user-supplied argument to its raw wire integer.</summary>
    public abstract long ToRaw(string userValue);
}

public sealed class IntegerArgumentType(string name, DataEncoding encoding, bool signed = false)
    : ArgumentType(name, encoding)
{
    public bool Signed { get; } = signed;

    public override long ToRaw(string userValue) =>
        long.Parse(userValue, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class EnumeratedArgumentType(string name, DataEncoding encoding, IReadOnlyDictionary<string, long> labels)
    : ArgumentType(name, encoding)
{
    public IReadOnlyDictionary<string, long> Labels { get; } = labels;

    public override long ToRaw(string userValue) =>
        Labels.TryGetValue(userValue, out var raw)
            ? raw
            : throw new ArgumentException($"'{userValue}' is not a valid value for argument type '{Name}'.");
}

/// <summary>An argument of a MetaCommand.</summary>
public sealed record CommandArgument(string Name, ArgumentType Type);

/// <summary>An entry in a CommandContainer.</summary>
public abstract record CommandEntry;

/// <summary>A fixed binary value contributed to the command container.</summary>
public sealed record FixedValueEntry(byte[] Value, int SizeInBits) : CommandEntry;

/// <summary>A reference to an argument, encoded per its argument type.</summary>
public sealed record ArgumentRefEntry(string ArgumentName) : CommandEntry;

/// <summary>
/// XTCE MetaCommand: an argument list and a command container describing the
/// binary layout. Source: OMG XTCE 1.3 (MetaCommand, CommandContainer).
/// </summary>
public sealed class MetaCommand(
    string name,
    string qualifiedName,
    IReadOnlyList<CommandArgument> arguments,
    IReadOnlyList<CommandEntry> entries)
{
    public string Name { get; } = name;

    public string QualifiedName { get; } = qualifiedName;

    public IReadOnlyList<CommandArgument> Arguments { get; } = arguments;

    public IReadOnlyList<CommandEntry> Entries { get; } = entries;
}
