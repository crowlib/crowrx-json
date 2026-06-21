# CrowRx.Json 1.x benchmark baseline

Measured on 2026-06-20 with BenchmarkDotNet 0.13.2, .NET 8.0.28, Windows 11,
and an AMD Ryzen 5 5600X. The `ShortRun` job used one launch, three warmups,
and three measured iterations.

| Method | Mean | Allocated |
| --- | ---: | ---: |
| `SerializeFlatDto` | 892.0 ns | 816 B |
| `DeserializeFlatDto` | 1,283.7 ns | 664 B |

These are Phase 0 comparison values, not performance guarantees. Re-run with:

```powershell
rtk dotnet run --project src/CrowRx.Json.Benchmarks/CrowRx.Json.Benchmarks.csproj --configuration Release -- --filter '*' --job short
```
