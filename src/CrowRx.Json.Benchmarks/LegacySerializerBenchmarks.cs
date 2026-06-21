using BenchmarkDotNet.Attributes;

namespace CrowRx.Json.Benchmarks;

[MemoryDiagnoser]
public class LegacySerializerBenchmarks
{
    private readonly FlatDto _value = new()
    {
        Id = 42,
        Name = "phase-zero",
        Score = 123.5,
    };

    private readonly string _json = "{\"Id\":42,\"Name\":\"phase-zero\",\"Score\":123.5}";

    [Benchmark]
    public string SerializeFlatDto() => _value.ToJson();

    [Benchmark]
    public FlatDto DeserializeFlatDto() => _json.ConvertFromJson<FlatDto>();

    public sealed class FlatDto
    {
        public int Id;
        public string Name = string.Empty;
        public double Score;
    }
}
