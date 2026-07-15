namespace Tranquility.Core.Mdb;

/// <summary>
/// XTCE-derived mission database model: space system tree with parameter,
/// parameter-type, and container lookups by qualified name.
/// Implements: L2-MDB-001, L2-MDB-002. Source: OMG XTCE 1.3.
/// </summary>
public sealed class MissionDatabase
{
    private readonly Dictionary<string, Parameter> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SequenceContainer> _containers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParameterType> _parameterTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<SequenceContainer, List<SequenceContainer>> _derived = new();

    public MissionDatabase(SpaceSystem root)
    {
        Root = root;
        Index(root);
        foreach (var container in _containers.Values)
        {
            if (container.BaseContainer is { } baseContainer)
            {
                if (!_derived.TryGetValue(baseContainer, out var list))
                {
                    _derived[baseContainer] = list = new List<SequenceContainer>();
                }

                list.Add(container);
            }
        }
    }

    public SpaceSystem Root { get; }

    public IReadOnlyCollection<Parameter> Parameters => _parameters.Values;

    public IReadOnlyCollection<SequenceContainer> Containers => _containers.Values;

    public IReadOnlyCollection<ParameterType> ParameterTypes => _parameterTypes.Values;

    public Parameter? FindParameter(string qualifiedName) =>
        _parameters.GetValueOrDefault(qualifiedName);

    public SequenceContainer? FindContainer(string qualifiedName) =>
        _containers.GetValueOrDefault(qualifiedName);

    public ParameterType? FindParameterType(string qualifiedName) =>
        _parameterTypes.GetValueOrDefault(qualifiedName);

    /// <summary>Containers that directly extend <paramref name="baseContainer"/>.</summary>
    public IReadOnlyList<SequenceContainer> GetDerivedContainers(SequenceContainer baseContainer) =>
        _derived.TryGetValue(baseContainer, out var list) ? list : Array.Empty<SequenceContainer>();

    /// <summary>Root containers (no base container) that packets are matched against.</summary>
    public IEnumerable<SequenceContainer> RootContainers =>
        _containers.Values.Where(c => c.BaseContainer is null);

    private void Index(SpaceSystem system)
    {
        foreach (var parameterType in system.ParameterTypes)
        {
            _parameterTypes[parameterType.QualifiedName] = parameterType;
        }

        foreach (var parameter in system.Parameters)
        {
            _parameters[parameter.QualifiedName] = parameter;
        }

        foreach (var container in system.Containers)
        {
            _containers[container.QualifiedName] = container;
        }

        foreach (var child in system.Children)
        {
            Index(child);
        }
    }
}

/// <summary>
/// A node in the XTCE space system hierarchy.
/// Source: OMG XTCE 1.3 (SpaceSystem element).
/// </summary>
public sealed class SpaceSystem
{
    public SpaceSystem(string name, SpaceSystem? parent = null)
    {
        Name = name;
        Parent = parent;
        QualifiedName = parent is null ? $"/{name}" : $"{parent.QualifiedName}/{name}";
        parent?.Children.Add(this);
    }

    public string Name { get; }

    public string QualifiedName { get; }

    public SpaceSystem? Parent { get; }

    public List<SpaceSystem> Children { get; } = new();

    public List<ParameterType> ParameterTypes { get; } = new();

    public List<Parameter> Parameters { get; } = new();

    public List<SequenceContainer> Containers { get; } = new();
}

/// <summary>
/// A named telemetry parameter.
/// Source: OMG XTCE 1.3 (Parameter element).
/// </summary>
public sealed class Parameter
{
    public Parameter(string name, string qualifiedName, ParameterType type, string? description = null)
    {
        Name = name;
        QualifiedName = qualifiedName;
        Type = type;
        Description = description;
    }

    public string Name { get; }

    public string QualifiedName { get; }

    public ParameterType Type { get; }

    public string? Description { get; }
}
