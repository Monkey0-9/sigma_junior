```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7462/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 8.0.12 (8.0.12, 8.0.1224.60305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.12 (8.0.12, 8.0.1224.60305), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error    | StdDev   | Allocated |
|----------------- |---------:|---------:|---------:|----------:|
| TryWrite_TryRead | 10.43 ns | 0.165 ns | 0.146 ns |         - |
