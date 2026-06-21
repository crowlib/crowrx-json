using CrowRx.Json;

var source = new SmokeDto { Id = 42, Name = "package-smoke" };
string json = source.ToJson();

if (!json.TryConvertFromJson(out SmokeDto result) || result.Id != source.Id || result.Name != source.Name)
{
    throw new InvalidOperationException("CrowRx.Json package round-trip smoke test failed.");
}

if (!JsonSerializer.TryDeserialize(json, out SmokeDto? typedResult, out JsonError typedError) ||
    typedError.Category != JsonErrorCategory.None ||
    typedResult is null ||
    typedResult.Id != source.Id ||
    typedResult.Name != source.Name)
{
    throw new InvalidOperationException("CrowRx.Json typed package smoke test failed.");
}

if (JsonSerializer.TryDeserialize<int>("\"not-an-int\"", out _, out JsonError conversionError) ||
    conversionError.Category != JsonErrorCategory.TypeMismatch ||
    conversionError.Path != "$")
{
    throw new InvalidOperationException("CrowRx.Json typed package error smoke test failed.");
}

var immutable = new ImmutableSmokeDto(7, "constructor");
ImmutableSmokeDto? immutableResult = JsonSerializer.Deserialize<ImmutableSmokeDto>(JsonSerializer.Serialize(immutable));
if (immutableResult is null || immutableResult.Id != immutable.Id || immutableResult.Name != immutable.Name)
{
    throw new InvalidOperationException("CrowRx.Json constructor-binding package smoke test failed.");
}

if ("1 trailing".TryConvertFromJson(out int _))
{
    throw new InvalidOperationException("CrowRx.Json package accepted trailing non-JSON content.");
}

var overwriteTarget = new SmokeDto { Id = 1, Name = "preserved" };
if (!JsonSerializer.TryOverwrite("""{"Id":99}""", overwriteTarget, out JsonError overwriteError) ||
    overwriteError.Category != JsonErrorCategory.None ||
    overwriteTarget.Id != 99 ||
    overwriteTarget.Name != "preserved")
{
    throw new InvalidOperationException("CrowRx.Json overwrite package smoke test failed.");
}

JsonSerializerOptions options = new JsonSerializerOptions(
        propertyNameCaseInsensitive: true,
        rejectUnknownMembers: true,
        allowNumericEnums: true)
    .WithConverter(new SmokeTokenConverter());
var optionTarget = new OptionSmokeDto();
if (!JsonSerializer.TryOverwrite("""{"token":5,"mode":2}""", optionTarget, out JsonError optionError, options: options) ||
    optionError.Category != JsonErrorCategory.None ||
    optionTarget.Token.Value != 5 ||
    optionTarget.Mode != SmokeMode.Two)
{
    throw new InvalidOperationException("CrowRx.Json options/converter package smoke test failed.");
}

Console.WriteLine(json);

public sealed class SmokeDto
{
    public int Id;
    public string Name = string.Empty;
}

public sealed class ImmutableSmokeDto
{
    [JsonPropertyName("id")]
    public int Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    public ImmutableSmokeDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

public readonly record struct SmokeToken(int Value);

public enum SmokeMode
{
    One = 1,
    Two = 2,
}

public sealed class OptionSmokeDto
{
    public SmokeToken Token { get; set; }
    public SmokeMode Mode { get; set; }
}

public sealed class SmokeTokenConverter : IJsonConverter<SmokeToken>
{
    public string Write(SmokeToken value) => value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public SmokeToken Read(string json) => new(int.Parse(json, System.Globalization.CultureInfo.InvariantCulture));
}
