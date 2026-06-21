using BenchmarkDotNet.Attributes;

namespace CrowRx.Json.Benchmarks;

[MemoryDiagnoser]
public class OverwriteBenchmarks
{
    private const string Json = """{"Id":42,"Name":"phase-six","Score":123.5}""";
    private readonly FlatDto _target = new();

    [Benchmark(Baseline = true)]
    public FlatDto DeserializeReplacement() => JsonSerializer.Deserialize<FlatDto>(Json)!;

    [Benchmark]
    public bool StagedOverwrite() => JsonSerializer.TryOverwrite(Json, _target, out _);

    public sealed class FlatDto
    {
        public int Id;
        public string Name = string.Empty;
        public double Score;
    }
}
