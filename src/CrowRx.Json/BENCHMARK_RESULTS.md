# CrowRx.Json 2.x benchmark results

Measured on 2026-06-21 with BenchmarkDotNet 0.13.2, .NET 8.0.28, Windows 11 and an AMD Ryzen 5 5600X. ShortRun uses one launch, three warmups and three measured iterations. These values are diagnostic snapshots, not performance guarantees.

## Flat comparison and 1.x reference

| Operation | 1.x stored baseline | CrowRx.Json 2.x | System.Text.Json |
| --- | ---: | ---: | ---: |
| Serialize flat DTO | 892.0 ns / 816 B | 675.5 ns / 808 B | 375.5 ns / 112 B |
| Deserialize flat DTO | 1,283.7 ns / 664 B | 1,362.9 ns / 1,944 B | 432.3 ns / 88 B |

The 1.x baseline was measured on the same machine during Phase 0. It was not rerun from a separate 1.x checkout.

## Representative 2.x results before writer pooling

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| Serialize nested DTO | 14,504.9 ns | 8,248 B |
| Deserialize nested DTO | 51,298.6 ns | 59,433 B |
| Serialize 256-item list | 11,132.2 ns | 10,824 B |
| Deserialize 256-item list | 71,691.1 ns | 60,201 B |
| Serialize 64-entry dictionary | 11,627.4 ns | 10,104 B |
| Deserialize 64-entry dictionary | 20,655.6 ns | 36,392 B |
| Deserialize immutable DTO | 904.8 ns | 1,720 B |

## Contract cache

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| Cold contract build | 5,013.9 ns | 3,992 B |
| Warm contract lookup | 8.5 ns | 0 B |

## Writer pooling decision

Profiling by allocation sites showed a new `StringBuilder` and reference-path `HashSet` for each write. Reusing only those two internal states reduced ShortRun allocations:

| Operation | Before | After |
| --- | ---: | ---: |
| Serialize flat DTO | 808 B | 288 B |
| Serialize nested DTO | 8,248 B | 5,328 B |
| Serialize 256-item list | 10,824 B | 8,096 B |
| Serialize 64-entry dictionary | 10,104 B | 7,376 B |

ShortRun timing variance did not justify a throughput claim. The pool retains builders only up to 32 KiB and contains no customization state.

Reader allocation remains dominated by the validated intermediate JSON tree, number text, path strings and DTO/collection staging. Replacing it requires a separately designed streaming typed reader; Phase 8 did not trade correctness for a broad rewrite.

Run the suite with:

```powershell
rtk dotnet run --project src/CrowRx.Json.Benchmarks/CrowRx.Json.Benchmarks.csproj --configuration Release -- --filter '*' --job short
```
