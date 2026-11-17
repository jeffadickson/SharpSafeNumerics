namespace SharpSafeNumerics;

/// <summary>Determines whether zero-initialized generated values may be used.</summary>
public enum DefaultValuePolicy : byte
{
    /// <summary>Require successful construction before reading or operating on the value.</summary>
    Invalid = 0,
    /// <summary>Interpret the default value as zero. The declared range must contain zero.</summary>
    ZeroValid = 1
}

/// <summary>Selects the invariant text accepted and written by generated bounded integers.</summary>
public enum IntegerTextPolicy : byte
{
    /// <summary>Parse invariant integer decimal text and format canonical decimal text.</summary>
    InvariantDecimal = 0,
    /// <summary>Parse hexadecimal digits and format uppercase hexadecimal text without a prefix.</summary>
    InvariantHexadecimal = 1
}

/// <summary>Selects the byte order of the generated fixed-width binary codec.</summary>
public enum IntegerEndianness : byte
{
    /// <summary>Write the least significant byte first.</summary>
    LittleEndian = 0,
    /// <summary>Write the most significant byte first.</summary>
    BigEndian = 1
}

/// <summary>
/// Declares an inclusive integer domain on a partial struct. Bounds are strings so all
/// built-in integer values, including 128-bit values, can be expressed as attribute arguments.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class BoundedIntegerAttribute : Attribute
{
    /// <summary>Create a bounded domain declaration.</summary>
    public BoundedIntegerAttribute(Type storageType, string minimum, string maximum)
    {
        StorageType = storageType;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Underlying built-in integer type.</summary>
    public Type StorageType { get; }

    /// <summary>Inclusive minimum in invariant decimal text.</summary>
    public string Minimum { get; }

    /// <summary>Inclusive maximum in invariant decimal text.</summary>
    public string Maximum { get; }

    /// <summary>Policy applied by generated Add and Subtract methods. Try methods always reject.</summary>
    public ArithmeticPolicy OverflowPolicy { get; set; } = ArithmeticPolicy.Reject;

    /// <summary>Determines whether the generated default value is invalid or represents zero.</summary>
    public DefaultValuePolicy DefaultPolicy { get; set; } = DefaultValuePolicy.Invalid;

    /// <summary>Parsing and formatting convention for generated invariant text APIs.</summary>
    public IntegerTextPolicy TextPolicy { get; set; } = IntegerTextPolicy.InvariantDecimal;

    /// <summary>
    /// Fixed binary encoding width in bytes. Zero selects the storage width, using four bytes for
    /// native integers in platform-neutral compilations. The entire declared range must fit.
    /// </summary>
    public int SerializationWidth { get; set; }

    /// <summary>Byte order for the generated binary codec.</summary>
    public IntegerEndianness Endianness { get; set; } = IntegerEndianness.LittleEndian;

    /// <summary>Optional display unit. It does not alter the stored value or perform conversion.</summary>
    public string? Unit { get; set; }
}

/// <summary>Marks a type or method as a strict numeric scope for analyzer enforcement.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StrictNumericAttribute : Attribute;
