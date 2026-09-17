# Auto-Patch Build Baseline

Verified upstream commit: `643c8f8b62cbb26fd3ac105b92497d9a9c445f08` (upstream 1.0.9.4)

The fork's own projects live under `src/`, alongside the upstream 1.x sources it patches.

## Layout

| What | Path |
|---|---|
| Patcher entry point | `src/WandEnhancer.Core/Core/Enhancer.cs` |
| Patch definitions | `src/WandEnhancer.Core/Core/EnhancerConfig.cs` |
| Shared models | `src/WandEnhancer.Core/Models/` |
| Tray auto-patch watcher | `src/WandEnhancer.AutoPatch/` |
| Tests | `src/WandEnhancer.Core.Tests/` |
| Installer | `src/installer/WandEnhancer.iss` |
| CI (mirror + release) | `.github/workflows/build-release.yml` |

## Patcher entry point signatures

```csharp
public class Enhancer
{
    public Enhancer(WeModConfig weModConfig, Action<string, ELogType> logger, PatchConfig config);
    public void Patch();
}
```

- `WeModConfig` — `src/WandEnhancer.Core/Models/WeModConfig.cs`
- `PatchConfig` — `src/WandEnhancer.Core/Models/PatchConfig.cs`
- `ELogType` — `src/WandEnhancer.Core/Models/ELogType.cs`

## Local build and test (Windows)

Requires VS 2019+ Build Tools with MSBuild, CMake, NuGet, Node.js >= 22.19, pnpm, and
Inno Setup 6 (installer step only). `src/build.ps1` runs the whole chain.

```bash
# native proxy DLL (embedded as a resource by WandEnhancer.Core)
cmake -S src/tools/asar-fuses-bypass -B src/.tmp/cmake/asar-fuses-bypass -A x64
cmake --build src/.tmp/cmake/asar-fuses-bypass --config Release

# web panel (must run before msbuild — its dist/ is embedded into the patcher)
cd src/web-panel && pnpm install --frozen-lockfile && pnpm build

# C# solution
cd src && ./.tmp/nuget.exe restore Wand-Enhancer.sln
"$MSBUILD" Wand-Enhancer.sln /m /p:Configuration=Release "/p:Platform=Any CPU" /t:Build

# tests (NUnit — no test runner ships in packages/)
nuget install NUnit.ConsoleRunner -Version 3.16.3 -OutputDirectory packages
packages/NUnit.ConsoleRunner.3.16.3/tools/nunit3-console.exe \
  WandEnhancer.Core.Tests/bin/Release/WandEnhancer.Core.Tests.dll
```

Node >= 22.19 is a hard floor: `@lingui/cli` 6.3 ESM-imports `globSync` from `node:fs`,
and `vite.config.ts` loads `@lingui/vite-plugin`. On Node 20 the config fails to load and
`vite build` aborts — which is what broke CI between 2026-09-05 and 2026-09-16.
