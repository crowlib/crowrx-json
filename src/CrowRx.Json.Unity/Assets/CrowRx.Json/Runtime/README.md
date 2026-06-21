# CrowRx.Json for Unity

Install the `CrowRx.Json` NuGet package with NuGetForUnity. The core assembly has no `UnityEngine` dependency.

For Medium or High managed stripping, preserve DTO members and constructors used through reflection. Use `[UnityEngine.Scripting.Preserve]` or a project `link.xml`, for example:

```xml
<linker>
  <assembly fullname="Game.Runtime">
    <type fullname="Game.SaveData" preserve="all" />
  </assembly>
</linker>
```

The package does not include converters for Unity value types. Register application-specific converters through immutable `JsonSerializerOptions`.
