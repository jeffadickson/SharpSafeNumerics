# Browser lab

The deep dive ships as a standalone guide and as part of this browser bundle.
Build from the checkout with the pinned .NET SDK:

    dotnet workload restore eng/DeepDiveWasm
    dotnet publish eng/DeepDiveWasm -c Release

Serve the published wwwroot over HTTP and open interactive-deep-dive.html or
index.html. Core CI uploads the same bundle. The Run C# button executes
SharpSafeNumerics Decimal64 operations through .NET WebAssembly. Edit the two
decimal operands and the rounding mode, one per line.

Monaco Editor 0.52.2 loads from jsDelivr, with a native text area fallback.
Source and MIT license: https://github.com/microsoft/monaco-editor/tree/v0.52.2
The lab accepts decimal operands and rounding modes. The C# implementation
is built with the application.
