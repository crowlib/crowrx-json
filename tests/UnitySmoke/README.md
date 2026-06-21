# Unity smoke test

This harness creates a disposable Unity project, copies the current CrowRx.Json sources into it, and builds/runs Windows Standalone players with High managed stripping for both Mono and IL2CPP. It does not open or modify `src/CrowRx.Json.Unity`.

```powershell
pwsh -NoProfile -File tests/UnitySmoke/Run-UnitySmoke.ps1 `
  -UnityPath "D:\Unity\Editor\6000.5.0f1\Editor\Unity.exe"
```

The runtime fixture covers mutable DTOs, constructor-bound immutable DTOs, immutable options, a custom converter, numeric enums and staged overwrite. `Assets/link.xml` preserves the fixture's reflection targets; removing it intentionally reproduces constructor stripping under High stripping.
