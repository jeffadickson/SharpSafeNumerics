namespace SharpSafeNumerics;

/// <summary>
/// Declares an inclusive integer domain on a partial struct. The source generator emits
/// storage and checked construction/arithmetic members. Bounds are strings so all fixed-width
/// integer literals, including 128-bit values, can be represented without attribute-argument loss.
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

    /// <summary>Underlying fixed-width integer type.</summary>
    public Type StorageType { get; }

    /// <summary>Inclusive minimum, parsed by the source generator.</summary>
    public string Minimum { get; }

    /// <summary>Inclusive maximum, parsed by the source generator.</summary>
    public string Maximum { get; }

    /// <summary>Default overflow policy for generated convenience methods.</summary>
    public ArithmeticPolicy OverflowPolicy { get; set; } = ArithmeticPolicy.Reject;
}

/// <summary>Marks a type or method as a strict numeric scope for analyzer enforcement.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StrictNumericAttribute : Attribute;
