namespace CrowRx.Json.Tests;

public sealed class ConstructorBindingTests
{
    [Fact]
    public void ImmutableClass_RoundTripsThroughConstructor()
    {
        var source = new ImmutableDto(7, "immutable");

        string json = JsonSerializer.Serialize(source);
        ImmutableDto? result = JsonSerializer.Deserialize<ImmutableDto>(json);

        Assert.Equal("{\"id\":7,\"name\":\"immutable\"}", json);
        Assert.NotNull(result);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Name, result.Name);
    }

    [Fact]
    public void ReadonlyField_IsSetOnlyByConstructor()
    {
        var source = new ReadonlyFieldDto(42);

        string json = source.ToJson();
        ReadonlyFieldDto? result = JsonSerializer.Deserialize<ReadonlyFieldDto>(json);

        Assert.Equal("{\"Value\":42}", json);
        Assert.NotNull(result);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void PositionalRecord_RoundTrips()
    {
        var source = new PositionRecord(3, "record");

        PositionRecord? result = JsonSerializer.Deserialize<PositionRecord>(JsonSerializer.Serialize(source));

        Assert.Equal(source, result);
    }

    [Fact]
    public void RegularRecordClass_RoundTripsThroughParameterlessConstruction()
    {
        var source = new RegularRecord { Value = 9, Name = "regular" };

        RegularRecord? result = JsonSerializer.Deserialize<RegularRecord>(JsonSerializer.Serialize(source));

        Assert.Equal(source, result);
    }

    [Fact]
    public void OptionalConstructorParameter_UsesDeclaredDefault()
    {
        OptionalDto? result = JsonSerializer.Deserialize<OptionalDto>("{\"Required\":5}");

        Assert.NotNull(result);
        Assert.Equal(5, result.Required);
        Assert.Equal("fallback", result.Label);
    }

    [Fact]
    public void MissingRequiredConstructorParameter_FailsAtParameterPath()
    {
        bool success = JsonSerializer.TryDeserialize<OptionalDto>("{\"Label\":\"set\"}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.MissingRequiredMember, error.Category);
        Assert.Equal("$.Required", error.Path);
        Assert.Equal(typeof(int), error.ExpectedType);
        Assert.Equal("Missing", error.ActualJsonType);
    }

    [Fact]
    public void ExplicitJsonConstructor_WinsOverPublicConstructors()
    {
        ExplicitDto? result = JsonSerializer.Deserialize<ExplicitDto>("{\"Value\":8}");

        Assert.NotNull(result);
        Assert.Equal(8, result.Value);
        Assert.Equal("explicit", result.Source);
    }

    [Fact]
    public void PublicParameterlessConstructor_WinsWhenSeveralPublicConstructorsExist()
    {
        ParameterlessPreferredDto? result = JsonSerializer.Deserialize<ParameterlessPreferredDto>("{\"Value\":6}");

        Assert.NotNull(result);
        Assert.Equal(6, result.Value);
        Assert.Equal("parameterless", result.Source);
    }

    [Fact]
    public void RemainingWritableMember_IsAssignedAfterConstructor()
    {
        RemainingMemberDto? result = JsonSerializer.Deserialize<RemainingMemberDto>("{\"Id\":1,\"Extra\":2}");

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(2, result.Extra);
        Assert.Equal(1, result.ExtraAssignmentCount);
    }

    [Fact]
    public void ConstructorConsumedWritableMember_IsNotAssignedAgain()
    {
        ConsumedWritableDto? result = JsonSerializer.Deserialize<ConsumedWritableDto>("{\"Id\":4}");

        Assert.NotNull(result);
        Assert.Equal(4, result.Id);
        Assert.Equal(0, result.AssignmentCount);
    }

    [Fact]
    public void AmbiguousPublicConstructors_FailDeterministically()
    {
        bool success = JsonSerializer.TryDeserialize<AmbiguousDto>("{\"Value\":1}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.AmbiguousConstructor, error.Category);
    }

    [Fact]
    public void ParameterMemberNameMismatch_FailsContractBinding()
    {
        bool success = JsonSerializer.TryDeserialize<NameMismatchDto>("{\"Value\":1}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.ConstructorBindingFailed, error.Category);
        Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterMemberTypeMismatch_FailsContractBinding()
    {
        bool success = JsonSerializer.TryDeserialize<TypeMismatchDto>("{\"Value\":1}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.ConstructorBindingFailed, error.Category);
        Assert.Contains("has type", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorException_IsConvertedToJsonError()
    {
        bool success = JsonSerializer.TryDeserialize<ThrowingDto>("{\"Value\":-1}", out _, out JsonError error);

        Assert.False(success);
        Assert.Equal(JsonErrorCategory.ConstructorInvocationFailed, error.Category);
        Assert.Equal("$", error.Path);
        Assert.Contains("Value must be non-negative", error.Message, StringComparison.Ordinal);
    }

    private sealed class ImmutableDto
    {
        [JsonPropertyName("id")]
        public int Id { get; }

        [JsonPropertyName("name")]
        public string Name { get; }

        public ImmutableDto(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    private sealed class ReadonlyFieldDto
    {
        public readonly int Value;

        public ReadonlyFieldDto(int Value)
        {
            this.Value = Value;
        }
    }

    private sealed record PositionRecord(int X, string Name);

    private sealed record RegularRecord
    {
        public int Value { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class OptionalDto
    {
        public int Required { get; }
        public string Label { get; }

        public OptionalDto(int Required, string Label = "fallback")
        {
            this.Required = Required;
            this.Label = Label;
        }
    }

    private sealed class ExplicitDto
    {
        public int Value { get; }
        public string Source { get; } = string.Empty;

        public ExplicitDto()
        {
            Value = -1;
            Source = "parameterless";
        }

        public ExplicitDto(string ignored)
        {
            Value = -2;
            Source = ignored;
        }

        [JsonConstructor]
        private ExplicitDto(int Value)
        {
            this.Value = Value;
            Source = "explicit";
        }
    }

    private sealed class ParameterlessPreferredDto
    {
        public int Value { get; set; }
        public string Source { get; } = string.Empty;

        public ParameterlessPreferredDto()
        {
            Source = "parameterless";
        }

        public ParameterlessPreferredDto(int Value)
        {
            this.Value = Value;
            Source = "parameterized";
        }
    }

    private sealed class RemainingMemberDto
    {
        private int _extra;

        public int Id { get; }
        public int Extra
        {
            get => _extra;
            set
            {
                _extra = value;
                ExtraAssignmentCount++;
            }
        }

        public int ExtraAssignmentCount { get; private set; }

        public RemainingMemberDto(int Id)
        {
            this.Id = Id;
        }
    }

    private sealed class AmbiguousDto
    {
        public int Value { get; }

        public AmbiguousDto(int Value)
        {
            this.Value = Value;
        }

        public AmbiguousDto(string Value)
        {
            this.Value = Value.Length;
        }
    }

    private sealed class ConsumedWritableDto
    {
        private int _id;

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                AssignmentCount++;
            }
        }

        public int AssignmentCount { get; private set; }

        public ConsumedWritableDto(int Id)
        {
            _id = Id;
        }
    }

    private sealed class NameMismatchDto
    {
        public int Value { get; }

        public NameMismatchDto(int value)
        {
            Value = value;
        }
    }

    private sealed class TypeMismatchDto
    {
        public int Value { get; }

        public TypeMismatchDto(long Value)
        {
            this.Value = checked((int)Value);
        }
    }

    private sealed class ThrowingDto
    {
        public int Value { get; }

        public ThrowingDto(int Value)
        {
            if (Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), "Value must be non-negative.");
            }

            this.Value = Value;
        }
    }
}
