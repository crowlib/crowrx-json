# Migrating CrowRx.Json 1.x to 2.x

CrowRx.Json 2.0 intentionally changes silent or ambiguous 1.x behavior. Treat the upgrade as a serialization-contract migration.

## Preferred APIs

| 1.x compatibility API | Preferred 2.x API |
| --- | --- |
| `value.ToJson()` | `JsonSerializer.Serialize(value)` |
| `json.ConvertFromJson<T>()` | `JsonSerializer.Deserialize<T>(json)` |
| `json.TryConvertFromJson(out value)` | `JsonSerializer.TryDeserialize(json, out value, out error)` |
| `TryConvertFromJsonOverwrite` | `JsonSerializer.TryOverwrite` |
| `isAppend` boolean | `JsonOverwriteMode.Replace` or `Append` |
| `ICustomActivator` overload | `JsonSerializerOptions.WithActivator` |

Compatibility extensions and `ICustomActivator` overloads remain available and route through the typed 2.x implementation. New code should use the structured error and options APIs.

## Behavior changes

- The parser consumes the complete input and rejects malformed separators, escapes, Unicode and numbers.
- Duplicate object keys fail instead of using the last value.
- Member and collection conversion failures fail the complete operation.
- Private members require `[JsonInclude]`; readonly values require constructor binding.
- Non-string dictionary keys and object-to-list coercion are unsupported without a converter.
- Unnamed enum output and numeric enum input fail by default.
- Cycles and maximum depth fail in a controlled manner.
- Generic overwrite mutates the supplied target and no longer replaces it with a hidden temporary object.

## Activators and converters

Use immutable options instead of a global registry:

```csharp
JsonSerializerOptions options = JsonSerializerOptions.Default
    .WithActivator(new WidgetActivator())
    .WithConverter(new UserIdConverter());
```

Registrations are exact-type and isolated per options snapshot. A converter is selected before reflection support is evaluated, so it can handle interfaces, abstract types or otherwise unsupported shapes.

## Unity stripping

2.x does not generate runtime accessors or use `Expression.Compile`. Unity builds that strip managed code must preserve DTO members and constructors reached through reflection with `[Preserve]` or `link.xml`.
