using System.Collections.Concurrent;
using System.Globalization;

namespace CrowRx.Json.Tests;

public sealed class Phase7OptionsTests
{
    [Fact]
    public void Converter_OverridesBuiltInConverter_AndDuplicateUserRegistrationFails()
    {
        var converter = new GuidAsNumberConverter();
        JsonSerializerOptions options = JsonSerializerOptions.Default.WithConverter(converter);
        Guid value = new Guid("00112233-4455-6677-8899-aabbccddeeff");

        Assert.Equal("17", JsonSerializer.Serialize(value, options));
        Assert.Equal(value, JsonSerializer.Deserialize<Guid>("17", options));
        Assert.Throws<InvalidOperationException>(() => options.WithConverter(converter));
    }

    [Fact]
    public void Converter_PrecedesUnsupportedContract_ForRootAndNestedValues()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default.WithConverter<IShape>(new ShapeConverter());
        var source = new ShapeHolder { Shape = new Shape(4) };

        string json = JsonSerializer.Serialize(source, options);
        ShapeHolder? result = JsonSerializer.Deserialize<ShapeHolder>(json, options);

        Assert.Equal("{\"Shape\":4}", json);
        Assert.Equal(4, Assert.IsType<Shape>(result!.Shape).Size);
    }

    [Fact]
    public void Options_AreImmutableAndIsolated()
    {
        JsonSerializerOptions baseline = new JsonSerializerOptions();
        JsonSerializerOptions insensitive = baseline.WithConverter(new WrappedNumberConverter());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WrappedNumber>("3", baseline));
        Assert.Equal(3, JsonSerializer.Deserialize<WrappedNumber>("3", insensitive).Value);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WrappedNumber>("3", JsonSerializerOptions.Default));
    }

    [Fact]
    public void MemberPolicies_ApplyToDeserializeAndOverwrite()
    {
        var permissive = new JsonSerializerOptions(propertyNameCaseInsensitive: true);
        var strict = new JsonSerializerOptions(propertyNameCaseInsensitive: true, rejectUnknownMembers: true);

        PolicyDto? read = JsonSerializer.Deserialize<PolicyDto>("{\"value\":2}", permissive);
        var target = new PolicyDto();
        Assert.True(JsonSerializer.TryOverwrite("{\"value\":3}", target, out _, options: permissive));
        Assert.False(JsonSerializer.TryOverwrite("{\"unknown\":1}", target, out JsonError error, options: strict));

        Assert.Equal(2, read!.Value);
        Assert.Equal(3, target.Value);
        Assert.Equal(JsonErrorCategory.UnknownMember, error.Category);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PolicyDto>("{\"unknown\":1}", strict));
    }

    [Fact]
    public void CaseInsensitiveMemberCollision_IsRejected()
    {
        var options = new JsonSerializerOptions(propertyNameCaseInsensitive: true);

        Assert.False(JsonSerializer.TryDeserialize<CaseCollisionDto>("{}", out _, out JsonError error, options));
        Assert.Equal(JsonErrorCategory.ConstructorBindingFailed, error.Category);
    }

    [Fact]
    public void NumericEnum_IsOptInAndRequiresDefinedInRangeValue()
    {
        var enabled = new JsonSerializerOptions(allowNumericEnums: true);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SmallEnum>("2"));
        Assert.Equal(SmallEnum.Two, JsonSerializer.Deserialize<SmallEnum>("2", enabled));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SmallEnum>("3", enabled));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SmallEnum>("256", enabled));
    }

    [Fact]
    public void TypedActivator_CreatesDtoAndLegacyActivatorOverloadUsesTypedPath()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default.WithActivator(new PrivateDtoActivator());
        PrivateDto? typed = JsonSerializer.Deserialize<PrivateDto>("{\"Value\":7}", options);

        var legacyActivator = new LegacyPrivateDtoActivator();
        bool legacySuccess = "{\"Value\":8}".TryConvertFromJson(out PrivateDto legacy, legacyActivator);

        Assert.Equal(7, typed!.Value);
        Assert.True(legacySuccess);
        Assert.Equal(8, legacy.Value);
        Assert.True(legacyActivator.ReceivedJsonDictionary);
    }

    [Fact]
    public void LegacyActivatorOverwriteOverload_PreservesTargetAndUsesTypedValidation()
    {
        var legacyActivator = new LegacyPrivateDtoActivator();
        PrivateDto target = PrivateDto.Create();
        target.Value = 1;

        Assert.True("{\"Value\":9}".TryConvertFromJsonOverwrite(ref target, legacyActivator));
        Assert.Equal(9, target.Value);
        Assert.False("{\"Value\":\"bad\"}".TryConvertFromJsonOverwrite(ref target, legacyActivator));
        Assert.Equal(9, target.Value);
    }

    [Fact]
    public void InvalidActivatorResult_FailsInsteadOfFallingBackToAConstructor()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default.WithActivator(new NullPolicyDtoActivator());

        Assert.False(JsonSerializer.TryDeserialize<PolicyDto>("{\"Value\":1}", out _, out JsonError error, options));
        Assert.Equal(JsonErrorCategory.ObjectCreationFailed, error.Category);
    }

    [Fact]
    public void Converter_IsUsedByOverwrite()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default.WithConverter(new WrappedNumberConverter());
        var target = new ConverterDto { Number = new WrappedNumber(1) };

        Assert.True(JsonSerializer.TryOverwrite("{\"Number\":9}", target, out _, options: options));
        Assert.Equal(9, target.Number.Value);
    }

    [Fact]
    public async Task SameOptions_AreSafeForParallelSerializeAndDeserialize()
    {
        JsonSerializerOptions options = new JsonSerializerOptions(propertyNameCaseInsensitive: true)
            .WithConverter(new WrappedNumberConverter());
        var failures = new ConcurrentQueue<string>();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(async index =>
        {
            await Task.Yield();
            var source = new ConverterDto { Number = new WrappedNumber(index) };
            string json = JsonSerializer.Serialize(source, options);
            ConverterDto? result = JsonSerializer.Deserialize<ConverterDto>(json, options);
            if (result?.Number.Value != index)
            {
                failures.Enqueue(json);
            }
        }));

        Assert.Empty(failures);
    }

    private sealed class GuidAsNumberConverter : IJsonConverter<Guid>
    {
        private static readonly Guid Value = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        public string Write(Guid value) => "17";
        public Guid Read(string json) => json == "17" ? Value : throw new FormatException();
    }

    private interface IShape { }
    private sealed record Shape(int Size) : IShape;
    private sealed class ShapeHolder { public IShape Shape { get; set; } = new Shape(0); }

    private sealed class ShapeConverter : IJsonConverter<IShape>
    {
        public string Write(IShape value) => ((Shape)value).Size.ToString(CultureInfo.InvariantCulture);
        public IShape Read(string json) => new Shape(int.Parse(json, CultureInfo.InvariantCulture));
    }

    private readonly record struct WrappedNumber(int Value);
    private sealed class WrappedNumberConverter : IJsonConverter<WrappedNumber>
    {
        public string Write(WrappedNumber value) => value.Value.ToString(CultureInfo.InvariantCulture);
        public WrappedNumber Read(string json) => new(int.Parse(json, CultureInfo.InvariantCulture));
    }

    private sealed class ConverterDto { public WrappedNumber Number { get; set; } }
    private sealed class PolicyDto { public int Value { get; set; } }
    private sealed class CaseCollisionDto
    {
        public int URL { get; set; }
        public int Url { get; set; }
    }
    private enum SmallEnum : byte { One = 1, Two = 2 }

    private sealed class PrivateDto
    {
        private PrivateDto() { }
        public int Value { get; set; }
        internal static PrivateDto Create() => new();
    }

    private sealed class PrivateDtoActivator : IJsonActivator<PrivateDto>
    {
        public PrivateDto Create() => PrivateDto.Create();
    }

    private sealed class NullPolicyDtoActivator : IJsonActivator<PolicyDto>
    {
        public PolicyDto Create() => null!;
    }

    private sealed class LegacyPrivateDtoActivator : ICustomActivator
    {
        public bool ReceivedJsonDictionary { get; private set; }
        public object CreateInstance(Type type) => PrivateDto.Create();
        public object CreateInstance(Type type, Dictionary<string, object> jsonDictionary)
        {
            ReceivedJsonDictionary = jsonDictionary.ContainsKey("Value");
            return PrivateDto.Create();
        }
    }
}
