```

BenchmarkDotNet v0.15.2, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]     : .NET 10.0.0 (10.0.25.38108), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.0 (10.0.25.38108), Arm64 RyuJIT AdvSIMD


```
| Method    | SystemType        | Mean     | Error    | StdDev   | Gen0   | Allocated |
|---------- |------------------ |---------:|---------:|---------:|-------:|----------:|
| **RunSystem** | **ClassSystem**       | **76.10 ns** | **1.330 ns** | **1.179 ns** | **0.0267** |     **168 B** |
| **RunSystem** | **ClassSystemStatic** | **75.56 ns** | **0.553 ns** | **0.490 ns** | **0.0267** |     **168 B** |
| **RunSystem** | **FuncSystem**        | **76.32 ns** | **0.419 ns** | **0.392 ns** | **0.0267** |     **168 B** |
| **RunSystem** | **FuncSystemStatic**  | **76.61 ns** | **0.880 ns** | **0.780 ns** | **0.0267** |     **168 B** |
