# Auto-Patch Build Baseline

Verified upstream commit: `643c8f8b62cbb26fd3ac105b92497d9a9c445f08`

## Patcher entry point signatures

```csharp
public class Enhancer
{
    public Enhancer(WeModConfig weModConfig, Action<string, ELogType> logger, PatchConfig config);
    public void Patch();
}
```

- `WeModConfig` is defined in `WandEnhancer/Models/WeModConfig.cs`.
- `PatchConfig` is defined in `WandEnhancer/Models/PatchConfig.cs`.
- `ELogType` is defined in `WandEnhancer/View/MainWindow/Logs.cs` (or referenced from `WandEnhancer.Utils`).
