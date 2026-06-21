# CrowRx.Json 2.x API

## JsonSerializer

- `Serialize<T>(T value, JsonSerializerOptions? options = null)` writes one complete JSON value.
- `Deserialize<T>` and the `Type` overload throw `JsonException` backed by `JsonError` on failure.
- `TryDeserialize<T>` and the `Type` overload return the same failure details without throwing.
- `TryOverwrite` stages replace or append mutations and reports `JsonError`.

The same options snapshot is used recursively for the root, members, collection elements and overwrite staging.

## JsonSerializerOptions

Constructor policies:

- `propertyNameCaseInsensitive`: ordinal case-insensitive DTO/constructor matching.
- `rejectUnknownMembers`: reject unmapped input in deserialize and overwrite.
- `allowNumericEnums`: accept only in-range integers that name a defined enum value.

Customization methods:

- `WithConverter<T>(IJsonConverter<T>)`
- `WithActivator<T>(IJsonActivator<T>)`

Both methods return a new immutable snapshot. Existing snapshots and `JsonSerializerOptions.Default` remain unchanged.

## IJsonConverter<T>

```csharp
public interface IJsonConverter<T>
{
    string Write(T value);
    T? Read(string json);
}
```

`Write` must return exactly one valid, complete JSON value. `Read` receives exactly one complete JSON value. Invalid output and converter exceptions become controlled serializer failures. A converter instance shared through options must itself be thread-safe.

Built-in converters handle `Guid` in D format and `DateTime`/`DateTimeOffset` with invariant round-trip ISO 8601 text. An exact-type user converter overrides a built-in converter.

## IJsonActivator<T>

`Create` supplies an instance for collection or DTO population when the default constructor rules are unsuitable. Returning `null` or an incompatible object fails with `ObjectCreationFailed`; the serializer does not silently fall back.

## Attributes

- `[JsonInclude]`
- `[JsonIgnore]`
- `[JsonPropertyName("name")]`
- `[JsonConstructor]`

## Error and overwrite semantics

`JsonError` includes category, path, source location when available, expected CLR type and actual JSON token kind. New-object deserialization never returns a partially populated success value.

Overwrite is staged but not fully transactional. Exceptions from user setters and custom mutable collections during the apply phase can leave earlier mutations in place.
