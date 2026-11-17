using System.Numerics;
using SharpSafeNumerics;

static class Program
{
    static int assertions;
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        // Exhaustive 8-bit unsigned addition, subtraction, multiplication, division.
        for (int ai = 0; ai <= byte.MaxValue; ai++)
        for (int bi = 0; bi <= byte.MaxValue; bi++)
        {
            byte a = (byte)ai, b = (byte)bi;
            var add = SafeInteger.TryAdd(a, b, out byte sum);
            Check(add == (ai + bi > 255 ? ArithmeticError.Overflow : ArithmeticError.None), "byte add status");
            if (add == ArithmeticError.None) Check(sum == ai + bi, "byte add result");
            var sub = SafeInteger.TrySubtract(a, b, out byte diff);
            Check(sub == (ai < bi ? ArithmeticError.Underflow : ArithmeticError.None), "byte sub status");
            if (sub == ArithmeticError.None) Check(diff == ai - bi, "byte sub result");
            var mul = SafeInteger.TryMultiply(a, b, out byte prod);
            Check(mul == (ai * bi > 255 ? ArithmeticError.Overflow : ArithmeticError.None), "byte multiply status");
            if (mul == ArithmeticError.None) Check(prod == ai * bi, "byte multiply result");
            var div = SafeInteger.TryDivide(a, b, out byte quotient);
            Check(div == (bi == 0 ? ArithmeticError.DivideByZero : ArithmeticError.None), "byte division status");
            if (bi != 0) Check(quotient == ai / bi, "byte division result");
        }

        // Exhaustive signed-byte arithmetic, compared to an independent int oracle.
        for (int ai = sbyte.MinValue; ai <= sbyte.MaxValue; ai++)
        for (int bi = sbyte.MinValue; bi <= sbyte.MaxValue; bi++)
        {
            sbyte a = (sbyte)ai, b = (sbyte)bi;
            var add = SafeInteger.TryAdd(a, b, out sbyte sum);
            Check(add == Expected(ai + bi), "sbyte add");
            if (add == ArithmeticError.None) Check(sum == ai + bi, "sbyte add value");
            var sub = SafeInteger.TrySubtract(a, b, out sbyte diff);
            Check(sub == Expected(ai - bi), "sbyte subtract");
            if (sub == ArithmeticError.None) Check(diff == ai - bi, "sbyte sub value");
            var mul = SafeInteger.TryMultiply(a, b, out sbyte product);
            Check(mul == Expected(ai * bi), "sbyte multiply");
            if (mul == ArithmeticError.None) Check(product == ai * bi, "sbyte multiply value");
        }

        Check(SafeInteger.TryAdd(int.MaxValue, 1, out int signedResult) == ArithmeticError.Overflow && signedResult == 0, "int overflow");
        Check(SafeInteger.TryAdd(int.MinValue, -1, out signedResult) == ArithmeticError.Underflow, "int underflow");
        Check(SafeInteger.TrySubtract(int.MinValue, 1, out signedResult) == ArithmeticError.Underflow, "sub underflow");
        Check(SafeInteger.TryMultiply(int.MinValue, -1, out signedResult) == ArithmeticError.Overflow, "multiply min");
        Check(SafeInteger.TryDivide(int.MinValue, -1, out signedResult) == ArithmeticError.Overflow, "divide min");
        Check(SafeInteger.TryNegate(int.MinValue, out signedResult) == ArithmeticError.Overflow, "negate min");
        Check(SafeInteger.TryNegate((uint)1, out uint u) == ArithmeticError.Underflow && u == 0, "unsigned negate");
        Check(SafeInteger.TryAdd(UInt128.MaxValue, (UInt128)1, out _) == ArithmeticError.Overflow, "UInt128 overflow");
        Check(SafeInteger.TryConvert<int, byte>(256, out _) == ArithmeticError.OutOfRange, "narrowing conversion");
        Check(SafeInteger.TryConvert<int, byte>(255, out byte conv) == ArithmeticError.None && conv == 255, "exact conversion");
        Check(SafeInteger.TryInRange(5, 0, 4, out _) == ArithmeticError.OutOfRange, "range");
        Check(SafeInteger.TryInRange(3, 0, 4, out int ranged) == ArithmeticError.None && ranged == 3, "range success");
        Check(SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Saturate).Value == 255, "saturate");
        Check(SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Wrap).Value == 4, "wrap");
        Check(!SafeInteger.Add((byte)250, (byte)10).IsSuccess, "reject");
        Check(!default(NumericResult<int>).IsSuccess, "default invalid");
        Check(new BigInteger(int.MaxValue) + 1 > int.MaxValue, "oracle sanity");
        Check(!default(Bounded<int, QueueRange>).IsValid, "bounded default must be invalid");
        Check(Bounded<int, QueueRange>.TryCreate(63, out var depth) == ArithmeticError.None, "bounded create");
        Check(depth.Value == 63, "bounded value");
        Check(depth.TryAdd(1, out var nextDepth) == ArithmeticError.OutOfRange, "bounded overflow");
        Check(!nextDepth.IsValid, "failed bounded operation returns invalid");
        Check(depth.TrySubtract(63, out nextDepth) == ArithmeticError.None && nextDepth.Value == 0, "bounded subtract");
        Check(nextDepth.TrySubtract(1, out _) == ArithmeticError.OutOfRange, "bounded range rejection");
        Check(Bounded<int, QueueRange>.TryCreate(64, out _) == ArithmeticError.OutOfRange, "bounded create fails");

        // Warm JIT paths before checking allocation; the test loop does no logging.
        for (int i = 0; i < 1000; i++)
        {
            _ = SafeInteger.TryAdd(10, 20, out _);
            _ = SafeInteger.TryAdd(int.MaxValue, 1, out _);
            _ = depth.TryAdd(1, out _);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++)
        {
            _ = SafeInteger.TryAdd(10, 20, out _);
            _ = SafeInteger.TryAdd(int.MaxValue, 1, out _);
            _ = depth.TryAdd(1, out _);
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "strict arithmetic allocated " + bytes + " bytes");

        Console.WriteLine("Passed " + assertions + " assertions.");
    }

    static ArithmeticError Expected(int value)
        => value > sbyte.MaxValue ? ArithmeticError.Overflow
         : value < sbyte.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
}

readonly struct QueueRange : IStaticIntegerRange<int>
{
    public static int Minimum => 0;
    public static int Maximum => 63;
}
