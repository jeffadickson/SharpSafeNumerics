// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using SharpSafeNumerics.Verification;

// Nonshipping entry point: no arguments validate the actual process width.
// Qualification passes --expected-bits explicitly to detect an unintended host.
return NativeWidthProbe.Run(args);

internal static class NativeWidthProbe
{
    /// <summary>Validates CLI and process width before executing the independent native-integer oracle.</summary>
    /// <returns>Zero on success, one on architecture/oracle failure, two on invalid arguments.</returns>
    internal static int Run(string[] args)
    {
        int expectedBits = IntPtr.Size * 8;
        if (args.Length != 0)
        {
            // Exact tokens reject signs, whitespace, leading zeroes and duplicate options.
            if (args.Length != 2 || args[0] != "--expected-bits" || args[1] is not ("32" or "64"))
            {
                Console.Error.WriteLine("Usage: NativeWidthProbe [--expected-bits 32|64]; no arguments assert the actual process width.");
                return 2;
            }
            expectedBits = args[1] == "32" ? 32 : 64;
        }

        int actualBits = IntPtr.Size * 8;
        Architecture architecture = RuntimeInformation.ProcessArchitecture;
        // Pointer width and runtime bitness must agree. A different 32-bit ISA does
        // not qualify the Windows x86 runtime even when its pointer width matches.
        if (actualBits != expectedBits || Environment.Is64BitProcess != (expectedBits == 64)
            || (expectedBits == 32 && architecture != Architecture.X86))
        {
            Console.Error.WriteLine($"Architecture mismatch: expected={expectedBits}; PointerBits={actualBits}; Architecture={architecture}; Is64BitProcess={Environment.Is64BitProcess}");
            return 1;
        }

        Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}; Architecture={architecture}; PointerBits={actualBits}; nint.Min={nint.MinValue}; nint.Max={nint.MaxValue}; nuint.Max={nuint.MaxValue}; Seed={WideIntegerOracle.Seed}");
        try
        {
            // Reuse the established exact reference and seed, not production arithmetic.
            var signed = WideIntegerOracle.Campaign<nint>();
            var unsigned = WideIntegerOracle.Campaign<nuint>();
            Console.WriteLine($"Passed nint boundaries={signed.Boundaries}, pairs={signed.Pairs}; nuint boundaries={unsigned.Boundaries}, pairs={unsigned.Pairs}");
            return 0;
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
