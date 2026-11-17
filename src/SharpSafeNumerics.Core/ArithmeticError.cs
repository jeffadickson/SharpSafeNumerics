namespace SharpSafeNumerics;

/// <summary>Uninitialized is zero so default(NumericResult&lt;T&gt;) never looks successful.</summary>
public enum ArithmeticError : byte
{
    Uninitialized = 0,
    None = 1,
    Overflow = 2,
    Underflow = 3,
    DivideByZero = 4,
    OutOfRange = 5
}

public enum ArithmeticPolicy : byte
{
    Reject = 0,
    Saturate = 1,
    Wrap = 2,
    Throw = 3
}
