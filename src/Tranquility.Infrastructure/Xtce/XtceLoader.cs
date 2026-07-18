using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Tranquility.Core.Alarms;
using Tranquility.Core.Mdb;

namespace Tranquility.Infrastructure.Xtce;

/// <summary>
/// Loads the supported XTCE subset into the Core mission database model with
/// exhaustive validation diagnostics.
/// Implements: L2-MDB-001 (two-phase reference validation, every broken
/// reference reported), L2-MDB-003 (AliasSet), L2-MDB-004 (explicit
/// unsupported-construct diagnostics with document location).
/// Source: OMG XTCE 1.3 (SpaceSystem, ParameterTypeSet, ParameterSet, ContainerSet).
///
/// Supported subset: Integer/Float/Enumerated parameter types, Integer/Float
/// data encodings, polynomial default calibrators, static alarm ranges,
/// sequence containers with ParameterRefEntry, container inheritance with
/// comparison restriction criteria, parameter aliases. Namespace-tolerant
/// (matches by local name) so XTCE 1.1/1.2/1.3 documents load identically.
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

    /// <summary>Loads and throws on the first error (Core-level convenience).</summary>
    public MissionDatabase Load()
    {
        var result = LoadWithDiagnostics();
        if (!result.Success)
        {
            throw new XtceLoadException(string.Join("; ", result.Errors.Select(e => e.Message)));
        }

        return result.Database!;
    }

    /// <summary>Loads, collecting every diagnostic instead of failing fast.</summary>
    public MdbLoadResult LoadWithDiagnostics()
    {
        byte[] bytes;
        using (var stream = _open())
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        var version = Convert.ToHexStringLower(SHA256.HashData(bytes))[..12];

        XDocument document;
        try
        {
            using var reader = new MemoryStream(bytes);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            return new MdbLoadResult(null,
                [new XtceDiagnostic(XtceDiagnosticSeverity.Error, $"XML parse failure: {e.Message}", null, e.LineNumber)]);
        }

        return Parse(document, version);
    }

    public static MdbLoadResult Parse(XDocument document, string version = "0")
    {
        var diagnostics = new List<XtceDiagnostic>();
        var rootElement = document.Root;
        if (rootElement is null || rootElement.Name.LocalName != "SpaceSystem")
        {
            diagnostics.Add(Error(
                $"Root element is '{rootElement?.Name.LocalName ?? "(none)"}', expected 'SpaceSystem'.",
                rootElement?.Name.LocalName, rootElement));
            return new MdbLoadResult(null, diagnostics);
        }

        var context = new LoadContext(diagnostics);
        var root = ParseSpaceSystem(rootElement, parent: null, context);
        context.ResolveContainers();

        return diagnostics.Any(d => d.Severity == XtceDiagnosticSeverity.Error)
            ? new MdbLoadResult(null, diagnostics)
            : new MdbLoadResult(new MissionDatabase(root, version), diagnostics);
    }

    private static SpaceSystem ParseSpaceSystem(XElement element, SpaceSystem? parent, LoadContext context)
    {
        string name = element.Attribute("name")?.Value ?? "(unnamed)";
        var system = new SpaceSystem(name, parent);

        var tm = Child(element, "TelemetryMetaData");
        if (tm is not null)
        {
            foreach (var typeElement in Child(tm, "ParameterTypeSet")?.Elements() ?? [])
            {
                try
                {
                    var parameterType = ParseParameterType(typeElement, system.QualifiedName);
                    system.ParameterTypes.Add(parameterType);
                    context.Types[parameterType.QualifiedName] = parameterType;
                }
                catch (XtceLoadException e)
                {
                    context.Diagnostics.Add(e.ToDiagnostic());
                }
            }

            foreach (var parameterElement in Child(tm, "ParameterSet")?.Elements() ?? [])
            {
                if (parameterElement.Name.LocalName != "Parameter")
                {
                    context.Diagnostics.Add(Error(
                        $"Unsupported construct '{parameterElement.Name.LocalName}' in ParameterSet.",
                        parameterElement.Name.LocalName, parameterElement));
                    continue;
                }

                try
                {
                    var parameter = ParseParameter(parameterElement, system.QualifiedName, context);
                    system.Parameters.Add(parameter);
                    context.Parameters[parameter.QualifiedName] = parameter;
                }
                catch (XtceLoadException e)
                {
                    context.Diagnostics.Add(e.ToDiagnostic());
                }
            }

            foreach (var containerElement in Child(tm, "ContainerSet")?.Elements() ?? [])
            {
                if (containerElement.Name.LocalName != "SequenceContainer")
                {
                    context.Diagnostics.Add(Error(
                        $"Unsupported construct '{containerElement.Name.LocalName}' in ContainerSet.",
                        containerElement.Name.LocalName, containerElement));
                    continue;
                }

                context.PendingContainers.Add((containerElement, system));
            }
        }

        var cm = Child(element, "CommandMetaData");
        if (cm is not null)
        {
            var argTypes = new Dictionary<string, ArgumentType>(StringComparer.Ordinal);
            foreach (var argTypeElement in Child(cm, "ArgumentTypeSet")?.Elements() ?? [])
            {
                try
                {
                    var argType = ParseArgumentType(argTypeElement);
                    argTypes[argType.Name] = argType;
                }
                catch (XtceLoadException e)
                {
                    context.Diagnostics.Add(e.ToDiagnostic());
                }
            }

            foreach (var metaCommandElement in Child(cm, "MetaCommandSet")?.Elements()
                         .Where(e => e.Name.LocalName == "MetaCommand") ?? [])
            {
                try
                {
                    system.Commands.Add(ParseMetaCommand(metaCommandElement, system.QualifiedName, argTypes));
                }
                catch (XtceLoadException e)
                {
                    context.Diagnostics.Add(e.ToDiagnostic());
                }
            }
        }

        foreach (var childElement in element.Elements().Where(e => e.Name.LocalName == "SpaceSystem"))
        {
            ParseSpaceSystem(childElement, system, context);
        }

        return system;
    }

    private static ArgumentType ParseArgumentType(XElement element)
    {
        string name = RequiredAttribute(element, "name");
        var encoding = ParseEncoding(element, name);
        switch (element.Name.LocalName)
        {
            case "IntegerArgumentType":
                return new IntegerArgumentType(name, encoding,
                    string.Equals(element.Attribute("signed")?.Value, "true", StringComparison.OrdinalIgnoreCase));
            case "EnumeratedArgumentType":
            {
                var labels = new Dictionary<string, long>(StringComparer.Ordinal);
                var list = Child(element, "EnumerationList")
                    ?? throw new XtceLoadException($"EnumeratedArgumentType '{name}' has no EnumerationList.", element);
                foreach (var enumeration in list.Elements().Where(e => e.Name.LocalName == "Enumeration"))
                {
                    labels[RequiredAttribute(enumeration, "label")] = ParseLong(RequiredAttribute(enumeration, "value"), enumeration);
                }

                return new EnumeratedArgumentType(name, encoding, labels);
            }

            default:
                throw new XtceLoadException(
                    $"Unsupported construct '{element.Name.LocalName}' in ArgumentTypeSet.", element, element.Name.LocalName);
        }
    }

    private static MetaCommand ParseMetaCommand(
        XElement element, string systemQualifiedName, Dictionary<string, ArgumentType> argTypes)
    {
        string name = RequiredAttribute(element, "name");
        string qualifiedName = $"{systemQualifiedName}/{name}";

        var arguments = new List<CommandArgument>();
        foreach (var argElement in Child(element, "ArgumentList")?.Elements().Where(e => e.Name.LocalName == "Argument") ?? [])
        {
            string argName = RequiredAttribute(argElement, "name");
            string typeRef = RequiredAttribute(argElement, "argumentTypeRef");
            if (!argTypes.TryGetValue(typeRef, out var type))
            {
                throw new XtceLoadException(
                    $"Argument '{argName}' references unknown argument type '{typeRef}'.", argElement, "Argument");
            }

            arguments.Add(new CommandArgument(argName, type));
        }

        var entries = new List<CommandEntry>();
        var container = Child(element, "CommandContainer");
        foreach (var entry in Child(container ?? element, "EntryList")?.Elements() ?? [])
        {
            switch (entry.Name.LocalName)
            {
                case "FixedValueEntry":
                    entries.Add(new FixedValueEntry(
                        Convert.FromHexString(RequiredAttribute(entry, "binaryValue")),
                        ParseInt(RequiredAttribute(entry, "sizeInBits"), entry)));
                    break;
                case "ArgumentRefEntry":
                    entries.Add(new ArgumentRefEntry(RequiredAttribute(entry, "argumentRef")));
                    break;
                default:
                    throw new XtceLoadException(
                        $"Unsupported construct '{entry.Name.LocalName}' in command EntryList.", entry, entry.Name.LocalName);
            }
        }

        return new MetaCommand(name, qualifiedName, arguments, entries);
    }

    private static ParameterType ParseParameterType(XElement element, string systemQualifiedName)
    {
        string localName = element.Name.LocalName;
        if (localName is not ("IntegerParameterType" or "FloatParameterType" or "EnumeratedParameterType"))
        {
            throw new XtceLoadException(
                $"Unsupported construct '{localName}' in ParameterTypeSet (outside the approved support matrix).",
                element, localName);
        }

        string name = RequiredAttribute(element, "name");
        string qualifiedName = $"{systemQualifiedName}/{name}";
        var encoding = ParseEncoding(element, qualifiedName);

        switch (localName)
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

            default:
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
                var other => throw new XtceLoadException($"Unsupported integer encoding '{other}'.", integer, "IntegerDataEncoding"),
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
                $"Parameter '{name}' references unknown parameter type '{typeRef}'.", element, "Parameter");
        }

        List<ParameterAlias>? aliases = null;
        var aliasSet = Child(element, "AliasSet");
        foreach (var alias in aliasSet?.Elements().Where(e => e.Name.LocalName == "Alias") ?? [])
        {
            (aliases ??= []).Add(new ParameterAlias(
                RequiredAttribute(alias, "nameSpace"), RequiredAttribute(alias, "alias")));
        }

        return new Parameter(name, $"{systemQualifiedName}/{name}", type,
            Child(element, "LongDescription")?.Value, aliases);
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

    private static XtceDiagnostic Error(string message, string? construct, XElement? element) =>
        new(XtceDiagnosticSeverity.Error, message, construct, LineOf(element));

    private static int? LineOf(XElement? element) =>
        element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;

    /// <summary>
    /// Container parsing is deferred so base containers and parameters defined
    /// anywhere in the document resolve regardless of declaration order — and
    /// so every broken reference is reported, not just the first.
    /// </summary>
    private sealed class LoadContext(List<XtceDiagnostic> diagnostics)
    {
        public List<XtceDiagnostic> Diagnostics { get; } = diagnostics;

        public Dictionary<string, ParameterType> Types { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Parameter> Parameters { get; } = new(StringComparer.Ordinal);

        public List<(XElement Element, SpaceSystem System)> PendingContainers { get; } = new();

        private readonly Dictionary<string, SequenceContainer> _built = new(StringComparer.Ordinal);

        public void ResolveContainers()
        {
            var byName = PendingContainers.ToDictionary(
                pending => $"{pending.System.QualifiedName}/{pending.Element.Attribute("name")?.Value}",
                pending => pending,
                StringComparer.Ordinal);

            foreach (var qualifiedName in byName.Keys)
            {
                try
                {
                    Build(qualifiedName, byName, new HashSet<string>(StringComparer.Ordinal));
                }
                catch (XtceLoadException e)
                {
                    Diagnostics.Add(e.ToDiagnostic());
                }
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
                throw new XtceLoadException($"Unknown container reference '{qualifiedName}'.", null, "BaseContainer");
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
                try
                {
                    baseContainer = Build(Qualify(baseRef, system.QualifiedName), byName, inProgress);
                }
                catch (XtceLoadException e) when (e.Element is null)
                {
                    // Re-anchor the unknown-reference finding on the referring element.
                    throw new XtceLoadException(
                        $"Container '{qualifiedName}' references unknown container '{baseRef}'.",
                        baseElement, "BaseContainer");
                }

                criteria = ParseCriteria(baseElement, system);
            }

            bool isAbstract = string.Equals(element.Attribute("abstract")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var container = new SequenceContainer(
                RequiredAttribute(element, "name"), qualifiedName, isAbstract, baseContainer, criteria);

            var entryList = element.Elements().FirstOrDefault(e => e.Name.LocalName == "EntryList");
            foreach (var entry in entryList?.Elements() ?? [])
            {
                if (entry.Name.LocalName != "ParameterRefEntry")
                {
                    Diagnostics.Add(Error(
                        $"Unsupported construct '{entry.Name.LocalName}' in EntryList of '{qualifiedName}'.",
                        entry.Name.LocalName, entry));
                    continue;
                }

                string parameterRef = RequiredAttribute(entry, "parameterRef");
                string parameterQualifiedName = Qualify(parameterRef, system.QualifiedName);
                if (!Parameters.TryGetValue(parameterQualifiedName, out var parameter))
                {
                    Diagnostics.Add(Error(
                        $"Container '{qualifiedName}' references unknown parameter '{parameterRef}'.",
                        "ParameterRefEntry", entry));
                    continue;
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
            foreach (var comparison in restriction.DescendantsAndSelf().Where(e => e.Name.LocalName == "Comparison"))
            {
                string parameterRef = RequiredAttribute(comparison, "parameterRef");
                string parameterQualifiedName = Qualify(parameterRef, system.QualifiedName);
                if (!Parameters.TryGetValue(parameterQualifiedName, out var parameter))
                {
                    Diagnostics.Add(Error(
                        $"RestrictionCriteria references unknown parameter '{parameterRef}'.",
                        "Comparison", comparison));
                    continue;
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

/// <summary>
/// Raised for one unloadable XTCE finding; convertible to the diagnostic
/// carried by validation reports (L2-MDB-001, L2-MDB-004).
/// </summary>
public sealed class XtceLoadException : Exception
{
    public XtceLoadException(string message, XElement? element = null, string? construct = null)
        : base(message)
    {
        Element = element;
        Construct = construct;
        Line = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null;
    }

    public XElement? Element { get; }

    public string? Construct { get; }

    public int? Line { get; }

    public XtceDiagnostic ToDiagnostic() =>
        new(XtceDiagnosticSeverity.Error, Message, Construct, Line);
}
