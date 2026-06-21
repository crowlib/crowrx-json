using BenchmarkDotNet.Attributes;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace CrowRx.Json.Benchmarks;

[MemoryDiagnoser]
public class SerializerComparisonBenchmarks
{
    private static readonly System.Text.Json.JsonSerializerOptions StjOptions = new()
    {
        IncludeFields = true,
    };

    private readonly FlatDto _flat = new() { Id = 42, Name = "phase-eight", Score = 123.5 };
    private readonly NestedDto _nested = CreateNested();
    private readonly List<int> _largeList = Enumerable.Range(0, 256).ToList();
    private readonly Dictionary<string, int> _dictionary = Enumerable.Range(0, 64)
        .ToDictionary(static value => $"key-{value:D2}", static value => value);
    private readonly ImmutableDto _immutable = new(42, "immutable");

    private readonly string _flatJson;
    private readonly string _nestedJson;
    private readonly string _largeListJson;
    private readonly string _dictionaryJson;
    private readonly string _immutableJson;

    public SerializerComparisonBenchmarks()
    {
        _flatJson = JsonSerializer.Serialize(_flat);
        _nestedJson = JsonSerializer.Serialize(_nested);
        _largeListJson = JsonSerializer.Serialize(_largeList);
        _dictionaryJson = JsonSerializer.Serialize(_dictionary);
        _immutableJson = JsonSerializer.Serialize(_immutable);
    }

    [Benchmark(Baseline = true)] public string CrowRxSerializeFlat() => JsonSerializer.Serialize(_flat);
    [Benchmark] public string StjSerializeFlat() => StjSerializer.Serialize(_flat, StjOptions);
    [Benchmark] public FlatDto CrowRxDeserializeFlat() => JsonSerializer.Deserialize<FlatDto>(_flatJson)!;
    [Benchmark] public FlatDto StjDeserializeFlat() => StjSerializer.Deserialize<FlatDto>(_flatJson, StjOptions)!;

    [Benchmark] public string CrowRxSerializeNested() => JsonSerializer.Serialize(_nested);
    [Benchmark] public string StjSerializeNested() => StjSerializer.Serialize(_nested, StjOptions);
    [Benchmark] public NestedDto CrowRxDeserializeNested() => JsonSerializer.Deserialize<NestedDto>(_nestedJson)!;
    [Benchmark] public NestedDto StjDeserializeNested() => StjSerializer.Deserialize<NestedDto>(_nestedJson, StjOptions)!;

    [Benchmark] public string CrowRxSerializeLargeList() => JsonSerializer.Serialize(_largeList);
    [Benchmark] public string StjSerializeLargeList() => StjSerializer.Serialize(_largeList, StjOptions);
    [Benchmark] public List<int> CrowRxDeserializeLargeList() => JsonSerializer.Deserialize<List<int>>(_largeListJson)!;
    [Benchmark] public List<int> StjDeserializeLargeList() => StjSerializer.Deserialize<List<int>>(_largeListJson, StjOptions)!;

    [Benchmark] public string CrowRxSerializeDictionary() => JsonSerializer.Serialize(_dictionary);
    [Benchmark] public string StjSerializeDictionary() => StjSerializer.Serialize(_dictionary, StjOptions);
    [Benchmark] public Dictionary<string, int> CrowRxDeserializeDictionary() => JsonSerializer.Deserialize<Dictionary<string, int>>(_dictionaryJson)!;
    [Benchmark] public Dictionary<string, int> StjDeserializeDictionary() => StjSerializer.Deserialize<Dictionary<string, int>>(_dictionaryJson, StjOptions)!;

    [Benchmark] public ImmutableDto CrowRxDeserializeImmutable() => JsonSerializer.Deserialize<ImmutableDto>(_immutableJson)!;
    [Benchmark] public ImmutableDto StjDeserializeImmutable() => StjSerializer.Deserialize<ImmutableDto>(_immutableJson, StjOptions)!;

    private static NestedDto CreateNested() => new()
    {
        Title = "root",
        Children = Enumerable.Range(0, 8).Select(index => new ChildDto
        {
            Id = index,
            Label = $"child-{index}",
            Values = Enumerable.Range(index, 16).ToArray(),
        }).ToList(),
    };

    public sealed class FlatDto
    {
        public int Id;
        public string Name = string.Empty;
        public double Score;
    }

    public sealed class NestedDto
    {
        public string Title { get; set; } = string.Empty;
        public List<ChildDto> Children { get; set; } = new();
    }

    public sealed class ChildDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public int[] Values { get; set; } = Array.Empty<int>();
    }

    public sealed record ImmutableDto(int Id, string Name);
}
