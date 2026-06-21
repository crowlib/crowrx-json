using BenchmarkDotNet.Attributes;

namespace CrowRx.Json.Benchmarks;

[MemoryDiagnoser]
public class ContractCacheBenchmarks
{
    private readonly TypeContractCache _warmCache = new();

    [GlobalSetup]
    public void Setup() => _warmCache.GetContract(typeof(ContractDto));

    [Benchmark]
    public object ColdContractBuild() => new TypeContractCache().GetContract(typeof(ContractDto));

    [Benchmark(Baseline = true)]
    public object WarmContractLookup() => _warmCache.GetContract(typeof(ContractDto));

    public sealed class ContractDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> Values { get; set; } = new();
        public NestedContractDto Nested { get; set; } = new();
    }

    public sealed class NestedContractDto
    {
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
