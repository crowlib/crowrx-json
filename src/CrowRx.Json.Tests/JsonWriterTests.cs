using System.Text.Json;

namespace CrowRx.Json.Tests;

public sealed class JsonWriterTests
{
    public static TheoryData<object, string> Scalars => new()
    {
        { true, "true" },
        { false, "false" },
        { sbyte.MinValue, "-128" },
        { byte.MaxValue, "255" },
        { short.MinValue, "-32768" },
        { ushort.MaxValue, "65535" },
        { int.MinValue, "-2147483648" },
        { uint.MaxValue, "4294967295" },
        { long.MinValue, "-9223372036854775808" },
        { ulong.MaxValue, "18446744073709551615" },
        { 1.5f, "1.5" },
        { 1.5d, "1.5" },
        { 123.45m, "123.45" },
        { 'x', "\"x\"" },
        { SampleEnum.Second, "\"Second\"" },
    };

    [Theory]
    [MemberData(nameof(Scalars))]
    public void Serialize_WritesSupportedScalar(object value, string expected)
    {
        Assert.Equal(expected, value.ToJson());
    }

    [Fact]
    public void Serialize_WritesNullNullableAndSpecialScalars()
    {
        int? nullable = 42;
        object? nullValue = null;
        var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var dateTime = new DateTime(2026, 6, 20, 1, 2, 3, DateTimeKind.Utc).AddTicks(4567);
        var dateTimeOffset = new DateTimeOffset(2026, 6, 20, 1, 2, 3, TimeSpan.FromHours(9)).AddTicks(4567);

        Assert.Equal("42", nullable.ToJson());
        Assert.Equal("null", ObjectExtension.ToJson(nullValue!));
        Assert.Equal("\"00112233-4455-6677-8899-aabbccddeeff\"", guid.ToJson());
        Assert.Equal($"\"{dateTime:O}\"", dateTime.ToJson());
        Assert.Equal($"\"{dateTimeOffset:O}\"", dateTimeOffset.ToJson());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Serialize_RejectsNonFiniteNumbers(object value)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => value.ToJson());

        Assert.Contains("not valid JSON numbers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_EscapesStringsAndPreservesValidUnicode()
    {
        const string source = "quote:\" slash:\\ controls:\b\f\n\r\t\u0001 한글 😀";

        string json = source.ToJson();

        Assert.Equal("\"quote:\\\" slash:\\\\ controls:\\b\\f\\n\\r\\t\\u0001 한글 😀\"", json);
        Assert.Equal(source, JsonSerializer.Deserialize<string>(json));
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void Serialize_RejectsUnpairedSurrogates(int codeUnit)
    {
        string value = new string((char)codeUnit, 1);

        Assert.Throws<InvalidOperationException>(() => value.ToJson());
    }

    [Fact]
    public void Serialize_WritesArraysListsAndOrdinallySortedDictionaries()
    {
        var value = new Dictionary<string, List<int>>
        {
            ["z"] = new List<int> { 3 },
            [""] = new List<int>(),
            ["a"] = new List<int> { 1, 2 },
        };

        string json = value.ToJson();

        Assert.Equal("{\"\":[],\"a\":[1,2],\"z\":[3]}", json);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(3, document.RootElement.GetProperty("z")[0].GetInt32());
        Assert.Equal("[1,2,3]", new[] { 1, 2, 3 }.ToJson());
    }

    [Fact]
    public void Serialize_UsesCachedContractOrderJsonNamesAndCanSerialize()
    {
        var value = new ContractDto
        {
            First = 1,
            Renamed = 2,
            WriteOnly = 3,
        };

        string first = value.ToJson();
        string second = value.ToJson();

        Assert.Equal("{\"First\":1,\"renamed\":2,\"privateValue\":4}", first);
        Assert.Equal(first, second);
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Equal(4, document.RootElement.GetProperty("privateValue").GetInt32());
    }

    [Fact]
    public void Serialize_WritesRepeatedReferencesButRejectsCycles()
    {
        var child = new ChildDto { Value = 7 };
        var repeated = new List<ChildDto> { child, child };
        var cyclic = new NodeDto();
        cyclic.Next = cyclic;

        Assert.Equal("[{\"Value\":7},{\"Value\":7}]", repeated.ToJson());
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => cyclic.ToJson());
        Assert.Contains("cyclic reference", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_RejectsValuesBeyondMaximumDepth()
    {
        var value = new NodeDto { Next = new NodeDto { Next = new NodeDto() } };
        var writer = new JsonWriter(new TypeContractCache(), maxDepth: 2);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => writer.Write(value));

        Assert.Contains("maximum serialization depth", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_RejectsUnsupportedCollectionsAndDictionaryKeys()
    {
        Assert.Throws<TypeContractException>(() => new HashSet<int> { 1 }.ToJson());
        Assert.Throws<TypeContractException>(() => new Dictionary<int, string> { [1] = "one" }.ToJson());
    }

    [Fact]
    public void Serialize_RejectsUnnamedEnumValues()
    {
        Assert.Throws<InvalidOperationException>(() => ((SampleEnum)99).ToJson());
    }

    public enum SampleEnum
    {
        First,
        Second,
    }

    private sealed class ContractDto
    {
        public int First;

        [JsonPropertyName("renamed")]
        public int Renamed { get; set; }

        public int WriteOnly
        {
            set { }
        }

        [JsonInclude]
        [JsonPropertyName("privateValue")]
        private int PrivateValue => 4;
    }

    private sealed class ChildDto
    {
        public int Value;
    }

    private sealed class NodeDto
    {
        public NodeDto? Next;
    }
}
