```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
Intel Core i5-10505 CPU 3.20GHz (Max: 3.19GHz), 1 CPU, 12 logical and 6 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method            | Input | Mean          | Error         | StdDev      | Median        | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------ |------ |--------------:|--------------:|------------:|--------------:|---------:|--------:|-------:|----------:|------------:|
| **ManualGuard**       | **-1**    |     **0.0000 ns** |     **0.0000 ns** |   **0.0000 ns** |     **0.0000 ns** |        **?** |       **?** |      **-** |         **-** |           **?** |
| SafeConversion    | -1    |     0.0019 ns |     0.0609 ns |   0.0033 ns |     0.0000 ns |        ? |       ? |      - |         - |           ? |
| CheckedConversion | -1    | 2,529.5337 ns | 3,651.9168 ns | 200.1739 ns | 2,542.0332 ns |        ? |       ? | 0.0496 |     320 B |           ? |
|                   |       |               |               |             |               |          |         |        |           |             |
| **ManualGuard**       | **255**   |     **0.5607 ns** |     **0.6348 ns** |   **0.0348 ns** |     **0.5773 ns** |    **1.003** |    **0.08** |      **-** |         **-** |          **NA** |
| SafeConversion    | 255   |     0.0000 ns |     0.0000 ns |   0.0000 ns |     0.0000 ns |    0.000 |    0.00 |      - |         - |          NA |
| CheckedConversion | 255   |     0.9066 ns |     6.6159 ns |   0.3626 ns |     1.0977 ns |    1.621 |    0.57 |      - |         - |          NA |
|                   |       |               |               |             |               |          |         |        |           |             |
| **ManualGuard**       | **256**   |     **0.6564 ns** |     **0.4394 ns** |   **0.0241 ns** |     **0.6531 ns** |     **1.00** |    **0.04** |      **-** |         **-** |          **NA** |
| SafeConversion    | 256   |     0.3907 ns |     4.4839 ns |   0.2458 ns |     0.2749 ns |     0.60 |    0.33 |      - |         - |          NA |
| CheckedConversion | 256   | 2,407.5537 ns | 7,307.6187 ns | 400.5552 ns | 2,398.8008 ns | 3,671.09 |  541.72 | 0.0496 |     320 B |          NA |
