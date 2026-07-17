using Tranquility.Core.Alarms;

namespace Tranquility.Core.Mdb;

/// <summary>
/// Base type for XTCE parameter types.
/// Source: OMG XTCE 1.3 (ParameterTypeSet).
/// </summary>
public abstract class ParameterType
{
    protected ParameterType(string name, string qualifiedName, DataEncoding encoding)
    {
        Name = name;
        QualifiedName = qualifiedName;
        Encoding = encoding;
    }

    public string Name { get; }

    public string QualifiedName { get; }

    /// <summary>Raw wire encoding of values of this type.</summary>
    public DataEncoding Encoding { get; }
}

/// <summary>
/// XTCE IntegerParameterType.
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class IntegerParameterType : ParameterType
{
    public IntegerParameterType(
        string name,
        string qualifiedName,
        DataEncoding encoding,
        bool signed = false,
        PolynomialCalibrator? calibrator = null,
        StaticAlarmRanges? defaultAlarm = null)
        : base(name, qualifiedName, encoding)
    {
        Signed = signed;
        Calibrator = calibrator;
        DefaultAlarm = defaultAlarm;
    }

    public bool Signed { get; }

    public PolynomialCalibrator? Calibrator { get; }

    public StaticAlarmRanges? DefaultAlarm { get; }
}

/// <summary>
/// XTCE FloatParameterType.
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class FloatParameterType : ParameterType
{
    public FloatParameterType(
        string name,
        string qualifiedName,
        DataEncoding encoding,
        PolynomialCalibrator? calibrator = null,
        StaticAlarmRanges? defaultAlarm = null)
        : base(name, qualifiedName, encoding)
    {
        Calibrator = calibrator;
        DefaultAlarm = defaultAlarm;
    }

    public PolynomialCalibrator? Calibrator { get; }

    public StaticAlarmRanges? DefaultAlarm { get; }
}

/// <summary>
/// XTCE EnumeratedParameterType.
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class EnumeratedParameterType : ParameterType
{
    public EnumeratedParameterType(
        string name,
        string qualifiedName,
        DataEncoding encoding,
        IReadOnlyDictionary<long, string> labels)
        : base(name, qualifiedName, encoding)
    {
        Labels = labels;
    }

    /// <summary>Raw value to label mapping (XTCE EnumerationList).</summary>
    public IReadOnlyDictionary<long, string> Labels { get; }

    public string GetLabel(long raw) =>
        Labels.TryGetValue(raw, out var label) ? label : $"UNDEF({raw})";
}

/// <summary>
/// Base type for XTCE data encodings.
/// Source: OMG XTCE 1.3 (DataEncoding elements).
/// </summary>
public abstract class DataEncoding
{
    protected DataEncoding(int sizeInBits)
    {
        SizeInBits = sizeInBits;
    }

    public int SizeInBits { get; }
}

public enum IntegerEncodingType
{
    Unsigned,
    TwosComplement,
}

/// <summary>
/// XTCE IntegerDataEncoding (big-endian bit field).
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class IntegerDataEncoding : DataEncoding
{
    public IntegerDataEncoding(int sizeInBits, IntegerEncodingType encoding = IntegerEncodingType.Unsigned)
        : base(sizeInBits)
    {
        if (sizeInBits is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBits), "Integer encodings of 1 to 64 bits are supported.");
        }

        Encoding = encoding;
    }

    public IntegerEncodingType Encoding { get; }
}

/// <summary>
/// XTCE FloatDataEncoding (IEEE 754, big-endian).
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class FloatDataEncoding : DataEncoding
{
    public FloatDataEncoding(int sizeInBits)
        : base(sizeInBits)
    {
        if (sizeInBits is not (32 or 64))
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBits), "IEEE 754 encodings of 32 or 64 bits are supported.");
        }
    }
}

/// <summary>
/// XTCE PolynomialCalibrator: eng = sum(c[i] * raw^i).
/// Source: OMG XTCE 1.3.
/// </summary>
public sealed class PolynomialCalibrator
{
    public PolynomialCalibrator(IReadOnlyList<double> coefficients)
    {
        if (coefficients.Count == 0)
        {
            throw new ArgumentException("A polynomial calibrator requires at least one coefficient.", nameof(coefficients));
        }

        Coefficients = coefficients;
    }

    /// <summary>Coefficients ordered by exponent: index 0 is the constant term.</summary>
    public IReadOnlyList<double> Coefficients { get; }

    public double Apply(double raw)
    {
        double result = 0;
        for (int i = Coefficients.Count - 1; i >= 0; i--)
        {
            result = result * raw + Coefficients[i];
        }

        return result;
    }
}
