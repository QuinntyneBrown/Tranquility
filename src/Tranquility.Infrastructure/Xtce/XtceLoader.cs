using System.Globalization;
using System.Xml.Linq;

using Tranquility.Core.Alarms;
using Tranquility.Core.Mdb;

namespace Tranquility.Infrastructure.Xtce;

/// <summary>
/// Loads the supported XTCE subset into the Core mission database model.
/// Implements: L2-MDB-001, L2-MDB-003 (validation errors are surfaced with element context).
/// Source: OMG XTCE 1.3 (SpaceSystem, ParameterTypeSet, ParameterSet, ContainerSet).
///
/// Supported subset: Integer/Float/Enumerated parameter types, Integer/Float data
/// encodings, polynomial default calibrators, static alarm ranges, sequence
/// containers with ParameterRefEntry, container inheritance with comparison
/// restriction criteria. Namespace-tolerant (matches by local name) so XTCE 1.1/1.2/1.3
/// documents load identically.
/// </summary>
public sealed class XtceLoader
{
    private readonly Func<Stream> _open;

    public XtceLoader(string path)
        : this(() => File.OpenRead(path))
    {
    }

    public XtceLoader(Func<Stream> open)
    {
        _open = open;
    }

    public MissionDatabase Load()
    {
        using var stream = _open();
        return Parse(XDocument.Load(stream, LoadOptions.SetLineInfo));
    }

    public static MissionDatabase Parse(XDocument document)
    {
        var rootElement = document.Root
            ?? throw new XtceLoadException("Document has no root element.");
        if (rootElement.Name.LocalName != "SpaceSystem")
        {
            throw new XtceLoadException($"Root element is '{rootElement.Name.LocalName}', expected 'SpaceSystem'.");
        }

        var context = new LoadContext();
        var root = ParseSpaceSystem(rootElement, parent: null, context);
        context.ResolveContainers();
        return new MissionDatabase(root);
    }

    private static SpaceSystem ParseSpaceSystem(XElement element, SpaceSystem? parent, LoadContext context)
    {
        string name = RequiredAttribute(element, "name");
        var system = new SpaceSystem(name, parent);

        var tm = Child(element, "TelemetryMetaData");
        if (tm is not null)
        {
            foreach (var typeElement in Child(tm, "ParameterTypeSet")?.Elements() ?? [])
            {
                var parameterType = ParseParameterType(typeElement, system.QualifiedName);
                system.ParameterTypes.Add(parameterType);
                context.Types[parameterType.QualifiedName] = parameterType;
            }

            foreach (var parameterElement in Child(tm, "ParameterSet")?.Elements() ?? [])
            {
                if (parameterElement.Name.LocalName != "Parameter")
                {
                    continue;
                }

                var parameter = ParseParameter(parameterElement, system.QualifiedName, context);
                system.Parameters.Add(parameter);
                context.Parameters[parameter.QualifiedName] = parameter;
            }

            foreach (var containerElement in Child(tm, "ContainerSet")?.Elements() ?? [])
            {
                if (containerElement.Name.LocalName != "SequenceContainer")
                {
                    continue;
                }

                context.PendingContainers.Add((containerElement, system));
            }
        }

        foreach (var childElement in element.Elements().Where(e => e.Name.LocalName == "SpaceSystem"))
        {
            ParseSpaceSystem(childElement, system, context);
        }

        return system;
    }

    private static ParameterType ParseParameterType(XElement element, string systemQualifiedName)
    {
        string name = RequiredAttribute(element, "name");
        string qualifiedName = $"{systemQualifiedName}/{name}";
        var encoding = ParseEncoding(element, qualifiedName);

        switch (element.Name.LocalName)
        {
            case "IntegerParameterType":
            {
                bool signed = string.Equals(element.Attribute("signed")?.Value, "true", StringComparison.OrdinalIgnoreCase);
                return new IntegerParameterType(
                    name, qualifiedName, encoding, signed,
                    ParseCalibrator(element), ParseAlarm(element));
            }

            case "FloatParameterType":
                return new FloatParameterType(
                    name, qualifiedName, encoding,
                    ParseCalibrator(element), ParseAlarm(element));

            case "EnumeratedParameterType":
            {
                var labels = new Dictionary<long, string>();
                var list = Child(element, "EnumerationList")
                    ?? throw new XtceLoadException($"EnumeratedParameterType '{qualifiedName}' has no EnumerationList.", element);
                foreach (var enumeration in list.Elements().Where(e => e.Name.LocalName == "Enumeration"))
                {
                    long value = ParseLong(RequiredAttribute(enumeration, "value"), enumeration);
                    labels[value] = RequiredAttribute(enumeration, "label");
                }

                return new EnumeratedParameterType(name, qualifiedName, encoding, labels);
            }

            default:
                throw new XtceLoadException($"Unsupported parameter type element '{element.Name.LocalName}'.", element);
        }
    }

    private static DataEncoding ParseEncoding(XElement typeElement, string qualifiedName)
    {
        var integer = Child(typeElement, "IntegerDataEncoding");
        if (integer is not null)
        {
            int sizeInBits = ParseInt(integer.Attribute("sizeInBits")?.Value ?? "8", integer);
            var encodingKind = integer.Attribute("encoding")?.Value switch
            {
                null or "unsigned" => IntegerEncodingType.Unsigned,
                "twosComplement" => IntegerEncodingType.TwosComplement,
                var other => throw new XtceLoadException($"Unsupported integer encoding '{other}'.", integer),
            };
            return new IntegerDataEncoding(sizeInBits, encodingKind);
        }

        var floatEncoding = Child(typeElement, "FloatDataEncoding");
        if (floatEncoding is not null)
        {
            int sizeInBits = ParseInt(floatEncoding.Attribute("sizeInBits")?.Value ?? "32", floatEncoding);
            return new FloatDataEncoding(sizeInBits);
        }

        throw new XtceLoadException($"Parameter type '{qualifiedName}' has no supported data encoding.", typeElement);
    }

    private static PolynomialCalibrator? ParseCalibrator(XElement typeElement)
    {
        var polynomial = Descendant(typeElement, "PolynomialCalibrator");
        if (polynomial is null)
        {
            return null;
        }

        var terms = new SortedDictionary<int, double>();
        foreach (var term in polynomial.Elements().Where(e => e.Name.LocalName == "Term"))
        {
            int exponent = ParseInt(RequiredAttribute(term, "exponent"), term);
            terms[exponent] = ParseDouble(RequiredAttribute(term, "coefficient"), term);
        }

        if (terms.Count == 0)
        {
            throw new XtceLoadException("PolynomialCalibrator has no terms.", polynomial);
        }

        int maxExponent = terms.Keys.Max();
        var coefficients = new double[maxExponent + 1];
        foreach (var (exponent, coefficient) in terms)
        {
            coefficients[exponent] = coefficient;
        }

        return new PolynomialCalibrator(coefficients);
    }

    private static StaticAlarmRanges? ParseAlarm(XElement typeElement)
    {
        var ranges = Descendant(typeElement, "StaticAlarmRanges");
        if (ranges is null)
        {
            return null;
        }

        return new StaticAlarmRanges
        {
            WatchRange = ParseRange(Child(ranges, "WatchRange")),
            WarningRange = ParseRange(Child(ranges, "WarningRange")),
            DistressRange = ParseRange(Child(ranges, "DistressRange")),
            CriticalRange = ParseRange(Child(ranges, "CriticalRange")),
            SevereRange = ParseRange(Child(ranges, "SevereRange")),
        };
    }

    private static AlarmRange? ParseRange(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        double? min = element.Attribute("minInclusive") is { } minAttr ? ParseDouble(minAttr.Value, element) : null;
        double? max = element.Attribute("maxInclusive") is { } maxAttr ? ParseDouble(maxAttr.Value, element) : null;
        return new AlarmRange(min, max);
    }

    private static Parameter ParseParameter(XElement element, string systemQualifiedName, LoadContext context)
    {
        string name = RequiredAttribute(element, "name");
        string typeRef = RequiredAttribute(element, "parameterTypeRef");
        string typeQualifiedName = Qualify(typeRef, systemQualifiedName);

        if (!context.Types.TryGetValue(typeQualifiedName, out var type))
        {
            throw new XtceLoadException(
                $"Parameter '{name}' references unknown parameter type '{typeRef}'.", element);
        }

        return new Parameter(name, $"{systemQualifiedName}/{name}", type, Child(element, "LongDescription")?.Value);
    }

    private static string Qualify(string reference, string systemQualifiedName) =>
        reference.StartsWith('/') ? reference : $"{systemQualifiedName}/{reference}";

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static XElement? Descendant(XElement parent, string localName) =>
        parent.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string RequiredAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
            ?? throw new XtceLoadException($"Element '{element.Name.LocalName}' is missing required attribute '{name}'.", element);

    private static int ParseInt(string text, XElement element) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new XtceLoadException($"'{text}' is not a valid integer.", element);

    private static long ParseLong(string text, XElement element) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : throw new XtceLoadException($"'{text}' is not a valid integer.", element);

    private static double ParseDouble(string text, XElement element) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new XtceLoadException($"'{text}' is not a valid number.", element);

    /// <summary>
    /// Container parsing is deferred so base containers and parameters defined
    /// anywhere in the document resolve regardless of declaration order.
    /// </summary>
    private sealed class LoadContext
    {
        public Dictionary<string, ParameterType> Types { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Parameter> Parameters { get; } = new(StringComparer.Ordinal);

        public List<(XElement Element, SpaceSystem System)> PendingContainers { get; } = new();

        private readonly Dictionary<string, SequenceContainer> _built = new(StringComparer.Ordinal);

        public void ResolveContainers()
        {
            var byName = PendingContainers.ToDictionary(
                pending => $"{pending.System.QualifiedName}/{RequiredAttribute(pending.Element, "name")}",
                pending => pending,
                StringComparer.Ordinal);

            foreach (var qualifiedName in byName.Keys)
            {
                Build(qualifiedName, byName, new HashSet<string>(StringComparer.Ordinal));
            }
        }

        private SequenceContainer Build(
            string qualifiedName,
            Dictionary<string, (XElement Element, SpaceSystem System)> byName,
            HashSet<string> inProgress)
        {
            if (_built.TryGetValue(qualifiedName, out var existing))
            {
                return existing;
            }

            if (!byName.TryGetValue(qualifiedName, out var pending))
            {
                throw new XtceLoadException($"Unknown container reference '{qualifiedName}'.");
            }

            if (!inProgress.Add(qualifiedName))
            {
                throw new XtceLoadException($"Container inheritance cycle involving '{qualifiedName}'.");
            }

            var (element, system) = pending;
            SequenceContainer? baseContainer = null;
            IReadOnlyList<RestrictionCriterion> criteria = Array.Empty<RestrictionCriterion>();

            var baseElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "BaseContainer");
            if (baseElement is not null)
            {
                string baseRef = RequiredAttribute(baseElement, "containerRef");
                baseContainer = Build(Qualify(baseRef, system.QualifiedName), byName, inProgress);
                criteria = ParseCriteria(baseElement, system);
            }

            bool isAbstract = string.Equals(element.Attribute("abstract")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var container = new SequenceContainer(
                RequiredAttribute(element, "name"), qualifiedName, isAbstract, baseContainer, criteria);

            var entryList = element.Elements().FirstOrDefault(e => e.Name.LocalName == "EntryList");
            foreach (var entry in entryList?.Elements().Where(e => e.Name.LocalName == "ParameterRefEntry") ?? [])
            {
                string parameterRef = RequiredAttribute(entry, "parameterRef");
                string parameterQualifiedName = Qualify(parameterRef, system.QualifiedName);
                if (!Parameters.TryGetValue(parameterQualifiedName, out var parameter))
                {
                    throw new XtceLoadException(
                        $"Container '{qualifiedName}' references unknown parameter '{parameterRef}'.", entry);
                }

                int? offset = null;
                var location = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "LocationInContainerInBits");
                if (location is not null)
                {
                    var fixedValue = location.Elements().FirstOrDefault(e => e.Name.LocalName == "FixedValue")
                        ?? throw new XtceLoadException("Only FixedValue container locations are supported.", location);
                    offset = ParseInt(fixedValue.Value, fixedValue);
                }

                container.Entries.Add(new ParameterEntry(parameter, offset));
            }

            system.Containers.Add(container);
            _built[qualifiedName] = container;
            inProgress.Remove(qualifiedName);
            return container;
        }

        private IReadOnlyList<RestrictionCriterion> ParseCriteria(XElement baseElement, SpaceSystem system)
        {
            var restriction = baseElement.Elements().FirstOrDefault(e => e.Name.LocalName == "RestrictionCriteria");
            if (restriction is null)
            {
                return Array.Empty<RestrictionCriterion>();
            }

            var criteria = new List<RestrictionCriterion>();
            var comparisons = restriction.Name.LocalName == "Comparison"
                ? [restriction]
                : restriction.Descendants().Where(e => e.Name.LocalName == "Comparison");
            foreach (var comparison in comparisons)
            {
                string parameterRef = RequiredAttribute(comparison, "parameterRef");
                string parameterQualifiedName = Qualify(parameterRef, system.QualifiedName);
                if (!Parameters.TryGetValue(parameterQualifiedName, out var parameter))
                {
                    throw new XtceLoadException(
                        $"RestrictionCriteria references unknown parameter '{parameterRef}'.", comparison);
                }

                var op = comparison.Attribute("comparisonOperator")?.Value switch
                {
                    null or "==" => ComparisonOperator.Equal,
                    "!=" => ComparisonOperator.NotEqual,
                    "<" => ComparisonOperator.LessThan,
                    "<=" => ComparisonOperator.LessThanOrEqual,
                    ">" => ComparisonOperator.GreaterThan,
                    ">=" => ComparisonOperator.GreaterThanOrEqual,
                    var other => throw new XtceLoadException($"Unsupported comparison operator '{other}'.", comparison),
                };

                bool useCalibrated = !string.Equals(
                    comparison.Attribute("useCalibratedValue")?.Value, "false", StringComparison.OrdinalIgnoreCase);

                criteria.Add(new RestrictionCriterion(
                    parameter, op, RequiredAttribute(comparison, "value"), useCalibrated));
            }

            return criteria;
        }
    }
}

/// <summary>Raised when an XTCE document cannot be loaded. Carries element line context (L2-MDB-003).</summary>
public sealed class XtceLoadException : Exception
{
    public XtceLoadException(string message, XElement? element = null)
        : base(Format(message, element))
    {
    }

    private static string Format(string message, XElement? element)
    {
        if (element is System.Xml.IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return $"{message} (line {lineInfo.LineNumber})";
        }

        return message;
    }
}
