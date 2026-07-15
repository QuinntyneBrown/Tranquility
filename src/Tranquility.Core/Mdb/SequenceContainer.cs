namespace Tranquility.Core.Mdb;

/// <summary>
/// XTCE SequenceContainer: an ordered list of parameter entries with optional
/// base-container inheritance and restriction criteria.
/// Implements: L2-MDB-002, L2-SPP-003. Source: OMG XTCE 1.3.
/// </summary>
public sealed class SequenceContainer
{
    public SequenceContainer(
        string name,
        string qualifiedName,
        bool isAbstract = false,
        SequenceContainer? baseContainer = null,
        IReadOnlyList<RestrictionCriterion>? restrictionCriteria = null)
    {
        Name = name;
        QualifiedName = qualifiedName;
        IsAbstract = isAbstract;
        BaseContainer = baseContainer;
        RestrictionCriteria = restrictionCriteria ?? Array.Empty<RestrictionCriterion>();
    }

    public string Name { get; }

    public string QualifiedName { get; }

    public bool IsAbstract { get; }

    /// <summary>Container this one extends, or null for a root container.</summary>
    public SequenceContainer? BaseContainer { get; }

    /// <summary>Conditions on already-extracted parameters that select this container.</summary>
    public IReadOnlyList<RestrictionCriterion> RestrictionCriteria { get; }

    /// <summary>Entries contributed by this container (excluding inherited entries).</summary>
    public List<ParameterEntry> Entries { get; } = new();

    /// <summary>The inheritance chain from the root container down to this container.</summary>
    public IReadOnlyList<SequenceContainer> GetInheritanceChain()
    {
        var chain = new List<SequenceContainer>();
        for (var c = this; c is not null; c = c.BaseContainer)
        {
            chain.Add(c);
        }

        chain.Reverse();
        return chain;
    }
}

/// <summary>
/// XTCE ParameterRefEntry. Entries are laid out sequentially after the previous
/// entry unless an absolute bit offset from container start is given.
/// Source: OMG XTCE 1.3 (EntryList).
/// </summary>
public sealed class ParameterEntry
{
    public ParameterEntry(Parameter parameter, int? absoluteBitOffset = null)
    {
        Parameter = parameter;
        AbsoluteBitOffset = absoluteBitOffset;
    }

    public Parameter Parameter { get; }

    /// <summary>Bit offset from container start, or null for sequential placement.</summary>
    public int? AbsoluteBitOffset { get; }
}

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

/// <summary>
/// XTCE Comparison inside RestrictionCriteria: compares an already-extracted
/// parameter value against a constant.
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class RestrictionCriterion
{
    public RestrictionCriterion(
        Parameter parameter,
        ComparisonOperator @operator,
        string value,
        bool useCalibratedValue = true)
    {
        Parameter = parameter;
        Operator = @operator;
        Value = value;
        UseCalibratedValue = useCalibratedValue;
    }

    public Parameter Parameter { get; }

    public ComparisonOperator Operator { get; }

    /// <summary>Comparison constant as written in XTCE (string form).</summary>
    public string Value { get; }

    /// <summary>When true, compares the calibrated value; otherwise the raw value.</summary>
    public bool UseCalibratedValue { get; }
}
