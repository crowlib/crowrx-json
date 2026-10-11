# CrowRx.Json

CrowRx.Json 2.x is a small deterministic JSON serializer for general-purpose C# DTOs. The core targets .NET Standard 2.1 and does not reference `UnityEngine`.

## Install

```xml
<PackageReference Include="CrowRx.Json" Version="2.0.1" />
```

Unity 6 users can install the same package through NuGetForUnity.

## Quick start

```csharp
var source = new SaveData { Id = 7, Name = "Crow" };
string json = JsonSerializer.Serialize(source);
SaveData? restored = JsonSerializer.Deserialize<SaveData>(json);

public sealed class SaveData
{
    public int Id;
    public string Name = string.Empty;
}
```

Failed conversions throw `JsonException`. Use `TryDeserialize` or `TryOverwrite` to receive a structured `JsonError` without exceptions.

## Options and converters

`JsonSerializerOptions` is immutable. Every `WithConverter` or `WithActivator` call returns an isolated snapshot that can be shared safely by concurrent serializer calls.

```csharp
JsonSerializerOptions options = new JsonSerializerOptions(
        propertyNameCaseInsensitive: true,
        rejectUnknownMembers: true,
        allowNumericEnums: true)
    .WithConverter(new UserIdConverter());

User? user = JsonSerializer.Deserialize<User>(json, options);
```

User converters take precedence over built-in converters and reflection contracts. Duplicate user registration for the same exact type is rejected.

## Supported core shapes

- JSON objects, arrays, strings, numbers, booleans and `null`
- Primitive numeric types, `char`, enums and nullable values
- `Guid`, `DateTime` and `DateTimeOffset`
- One-dimensional arrays, `List<T>`, `IList<T>`, and string-keyed dictionaries
- Mutable DTOs, constructor-bound immutable DTOs and records
- Explicit include, ignore, property-name and constructor attributes
- Staged replace/append overwrite

CrowRx.Json rejects malformed JSON, numeric overflow, duplicate object keys, cycles, unsupported collection shapes and non-finite floating-point values.

## Overwrite contract

Overwrite converts and stages the complete input before applying mutations. Conversion failures do not mutate the target. A user setter or custom collection can still throw during application; already-applied mutations are not rolled back.

## Unity and stripping

The core package has no `UnityEngine` reference and does not include Unity value-type converters. Register those in application-owned options.

Reflection targets must be preserved when managed stripping is enabled. Annotate DTOs with Unity's `Preserve` attribute or add a project `link.xml` rule for reflected members and constructors. Mono and IL2CPP High-stripping smoke instructions live in [`../../tests/UnitySmoke`](../../tests/UnitySmoke).

## More documentation

- [`API.md`](API.md)
- [`MIGRATION_1X_TO_2X.md`](MIGRATION_1X_TO_2X.md)
- [`SERIALIZATION_SPEC.md`](SERIALIZATION_SPEC.md)
- [`BENCHMARK_RESULTS.md`](BENCHMARK_RESULTS.md)
