namespace CrowRx.Json.Tests;

public sealed class JsonDeserializerTests
{
    public static TheoryData<string, Type, object> ScalarValues => new()
    {
        { "-128", typeof(sbyte), sbyte.MinValue },
        { "255", typeof(byte), byte.MaxValue },
        { "-32768", typeof(short), short.MinValue },
        { "65535", typeof(ushort), ushort.MaxValue },
        { "-2147483648", typeof(int), int.MinValue },
        { "4294967295", typeof(uint), uint.MaxValue },
        { "-9223372036854775808", typeof(long), long.MinValue },
        { "18446744073709551615", typeof(ulong), ulong.MaxValue },
        { "1.5", typeof(float), 1.5f },
        { "1.5", typeof(double), 1.5d },
        { "79228162514264337593543950335", typeof(decimal), decimal.MaxValue },
        { "true", typeof(bool), true },
        { "\"x\"", typeof(char), 'x' },
        { "\"text\"", typeof(string), "text" },
    };

    [Theory]
    [MemberData(nameof(ScalarValues))]
    public void TryDeserialize_ConvertsSupportedScalars(string json, Type targetType, object expected)
    {
        bool success = JsonSerializer.TryDeserialize(json, targetType, out object? value, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryDeserialize_ConvertsEnumNullableGuidAndDates()
    {
        var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var dateTime = new DateTime(2026, 6, 20, 1, 2, 3, DateTimeKind.Utc).AddTicks(4567);
        var dateTimeOffset = new DateTimeOffset(2026, 6, 20, 1, 2, 3, TimeSpan.FromHours(9)).AddTicks(4567);

        Assert.Equal(SampleEnum.Second, JsonSerializer.Deserialize<SampleEnum>("\"Second\""));
        Assert.Equal(42, JsonSerializer.Deserialize<int?>("42"));
        Assert.Null(JsonSerializer.Deserialize<int?>("null"));
        Assert.Null(JsonSerializer.Deserialize<string>("null"));
        Assert.Equal(guid, JsonSerializer.Deserialize<Guid>($"\"{guid:D}\""));
        Assert.Equal(dateTime, JsonSerializer.Deserialize<DateTime>($"\"{dateTime:O}\""));
        Assert.Equal(dateTimeOffset, JsonSerializer.Deserialize<DateTimeOffset>($"\"{dateTimeOffset:O}\""));
    }

    [Theory]
    [InlineData("128", typeof(sbyte))]
    [InlineData("-1", typeof(byte))]
    [InlineData("2147483648", typeof(int))]
    [InlineData("18446744073709551616", typeof(ulong))]
    [InlineData("3.5e39", typeof(float))]
    [InlineData("1e-50", typeof(float))]
    [InlineData("1e-400", typeof(double))]
    [InlineData("1e-100", typeof(decimal))]
    [InlineData("79228162514264337593543950336", typeof(decimal))]
    public void TryDeserialize_RejectsNumericOverflow(string json, Type targetType)
    {
        bool success = JsonSerializer.TryDeserialize(json, targetType, out object? value, out JsonError error);

        Assert.False(success);
        Assert.Null(value);
        Assert.Equal(JsonErrorCategory.NumericOverflow, error.Category);
        Assert.Equal(targetType, error.ExpectedType);
        Assert.Equal("Number", error.ActualJsonType);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1e0")]
    public void TryDeserialize_RejectsNonIntegerTokenForIntegerTarget(string json)
    {
        bool success = JsonSerializer.TryDeserialize<int>(json, out int value, out JsonError error);

        Assert.False(success);
        Assert.Equal(0, value);
        Assert.Equal(JsonErrorCategory.TypeMismatch, error.Category);
    }

    [Theory]
    [InlineData("null", typeof(int), JsonErrorCategory.TypeMismatch)]
    [InlineData("1", typeof(string), JsonErrorCategory.TypeMismatch)]
    [InlineData("\"second\"", typeof(SampleEnum), JsonErrorCategory.InvalidValue)]
    [InlineData("99", typeof(SampleEnum), JsonErrorCategory.InvalidValue)]
    public void TryDeserialize_RejectsIncompatibleTokens(string json, Type targetType, JsonErrorCategory category)
    {
        Assert.False(JsonSerializer.TryDeserialize(json, targetType, out _, out JsonError error));
        Assert.Equal(category, error.Category);
    }

    [Fact]
    public void TryDeserialize_CreatesNestedArraysListsAndDictionaries()
    {
        const string json = "{\"items\":[[1,2],[3]]}";

        Dictionary<string, List<int[]>>? result = JsonSerializer.Deserialize<Dictionary<string, List<int[]>>>(json);

        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 2 }, result["items"][0]);
        Assert.Equal(new[] { 3 }, result["items"][1]);
    }

    [Fact]
    public void TryDeserialize_CreatesSupportedCollectionInterfaces()
    {
        IList<int>? list = JsonSerializer.Deserialize<IList<int>>("[1,2]");
        IDictionary<string, int>? dictionary = JsonSerializer.Deserialize<IDictionary<string, int>>("{\"value\":3}");

        Assert.IsType<List<int>>(list);
        Assert.Equal(new[] { 1, 2 }, list);
        Assert.IsType<Dictionary<string, int>>(dictionary);
        Assert.Equal(3, dictionary["value"]);
    }

    [Fact]
    public void TryDeserialize_ReportsNestedCollectionFailurePath()
    {
        const string json = "{\"items\":[[1],[2,\"bad\"]]}";

        bool success = JsonSerializer.TryDeserialize<Dictionary<string, List<int[]>>>(json, out _, out JsonError error);

        Assert.False(success);
        Assert.Equal("$.items[1][1]", error.Path);
        Assert.Equal(typeof(int), error.ExpectedType);
        Assert.Equal("String", error.ActualJsonType);
    }

    [Fact]
    public void TryDeserialize_PopulatesMutableDtoAndKeepsMissingInitialValues()
    {
        const string json = "{\"Id\":7,\"jsonName\":\"set\",\"privateValue\":9,\"Unknown\":true}";

        bool success = JsonSerializer.TryDeserialize<MutableDto>(json, out MutableDto? result, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("set", result.Name);
        Assert.Equal(9, result.PrivateValue);
        Assert.Equal(11, result.Missing);
    }

    [Fact]
    public void TryDeserialize_DtoMemberFailureDoesNotExposePartialObject()
    {
        const string json = "{\"Id\":7,\"jsonName\":3}";

        bool success = JsonSerializer.TryDeserialize<MutableDto>(json, out MutableDto? result, out JsonError error);

        Assert.False(success);
        Assert.Null(result);
        Assert.Equal("$.jsonName", error.Path);
        Assert.Equal(JsonErrorCategory.TypeMismatch, error.Category);
    }

    [Fact]
    public void StrictDeserializer_RejectsUnknownMembers()
    {
        Assert.True(JsonReader.TryRead("{\"unknown\":1}", out object? tree, out _, preserveNumbers: true));
        var deserializer = new JsonDeserializer(new TypeContractCache(), rejectUnknownMembers: true);

        bool success = deserializer.TryConvertRoot(tree, typeof(MutableDto), out object? value, out JsonError error);

        Assert.False(success);
        Assert.Null(value);
        Assert.Equal(JsonErrorCategory.UnknownMember, error.Category);
        Assert.Equal("$.unknown", error.Path);
    }

    [Fact]
    public void TryDeserialize_UsesTheOnlyPublicParameterizedConstructor()
    {
        bool success = JsonSerializer.TryDeserialize<ConstructorDto>("{\"Value\":1}", out ConstructorDto? value, out JsonError error);

        Assert.True(success);
        Assert.Equal(JsonErrorCategory.None, error.Category);
        Assert.NotNull(value);
        Assert.Equal(1, value.Value);
    }

    [Fact]
    public void Deserialize_ThrowsJsonExceptionWithTheSameError()
    {
        JsonException exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<int>("\"bad\""));

        Assert.Equal(JsonErrorCategory.TypeMismatch, exception.Error.Category);
        Assert.Equal("$", exception.Error.Path);
    }

    [Fact]
    public void TryDeserialize_PreservesReaderErrorsAndLocations()
    {
        bool success = JsonSerializer.TryDeserialize<int>("1 trailing", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.TrailingContent, error.Category);
        Assert.Equal(2, error.Position);
        Assert.Equal(1, error.Line);
        Assert.Equal(3, error.Column);
    }

    public enum SampleEnum
    {
        First,
        Second,
    }

    private sealed class MutableDto
    {
        public int Id = 0;
        public int Missing = 11;

        [JsonPropertyName("jsonName")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude]
        [JsonPropertyName("privateValue")]
        private int _privateValue = 0;

        public int PrivateValue => _privateValue;
    }

    private sealed class ConstructorDto
    {
        public int Value;

        public ConstructorDto(int Value)
        {
            this.Value = Value;
        }
    }
}
