# Auto-Patch Wand-Enhancer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an automatic patching subsystem to Wand-Enhancer that detects Wand updates, terminates Wand processes, re-applies the existing patch, and ensures every Wand launch is patched first.

**Architecture:** Add a shared `WandEnhancer.Core` class library containing the patch logic, path detection, process termination, and settings store. Build a new lightweight `WandEnhancer.AutoPatch.exe` helper that runs in `--patch`, `--launch`, and `--watch` modes. Extend the existing WPF app with a setup flow that registers a replaced Wand shortcut and an elevated scheduled task. Both executables read the same `appsettings.json`.

**Tech Stack:** C# .NET Framework 4.8, WPF (existing), WinForms (helper), MSBuild, Windows Task Scheduler, Windows Registry, FileSystemWatcher, Newtonsoft.Json.

## Global Constraints

- Target framework is `.NET Framework 4.8`.
- Platform target is `x64`.
- The project must build with the existing `build.ps1` pipeline.
- Admin elevation is required once during setup.
- No network requests; all operations are local.
- Auto-patch reuses the same patch settings as the main UI (`appsettings.json`).
- All Wand processes must be terminated before patching.
- The helper must show a compact progress window during auto-patching.

---

## File Structure

### Existing files (will be modified)

- `Wand-Enhancer.sln` — add new projects.
- `WandEnhancer/WandEnhancer.csproj` — reference `WandEnhancer.Core`.
- `WandEnhancer/Program.cs` — no changes required; keep as-is.
- `WandEnhancer/View/MainWindow/MainWindowVm.cs` — add setup commands and auto-patch status.
- `WandEnhancer/View/MainWindow/MainWindow.xaml` — add Auto-Patch Setup button/status.
- `build.ps1` — build the new projects and copy `AutoPatch.exe` to the output.

### New files (will be created)

- `WandEnhancer.Core/WandEnhancer.Core.csproj` — shared class library.
- `WandEnhancer.Core/Models/PatchConfig.cs` — settings model.
- `WandEnhancer.Core/Models/WeModInfo.cs` — Wand install info (reuse existing if present).
- `WandEnhancer.Core/Services/IWeModLocator.cs` — locator interface.
- `WandEnhancer.Core/Services/WeModLocator.cs` — auto-detect + manual fallback.
- `WandEnhancer.Core/Services/IProcessManager.cs` — process termination interface.
- `WandEnhancer.Core/Services/ProcessManager.cs` — terminate Wand processes.
- `WandEnhancer.Core/Services/IPatcher.cs` — patcher interface.
- `WandEnhancer.Core/Services/Patcher.cs` — wraps existing patch logic.
- `WandEnhancer.Core/Services/ISettingsStore.cs` — settings store interface.
- `WandEnhancer.Core/Services/SettingsStore.cs` — thread-safe `appsettings.json` I/O.
- `WandEnhancer.Core/Services/ILogger.cs` — logger interface.
- `WandEnhancer.Core/Services/FileLogger.cs` — file logger.
- `WandEnhancer.Core/Extensions/PathExtensions.cs` — validation helpers.
- `WandEnhancer.AutoPatch/WandEnhancer.AutoPatch.csproj` — helper executable.
- `WandEnhancer.AutoPatch/Program.cs` — argument parsing and mode dispatch.
- `WandEnhancer.AutoPatch/PatchModeController.cs` — `--patch` implementation.
- `WandEnhancer.AutoPatch/LaunchModeController.cs` — `--launch` implementation.
- `WandEnhancer.AutoPatch/WatchModeController.cs` — `--watch` implementation.
- `WandEnhancer.AutoPatch/ProgressWindow.cs` — compact WinForms progress window.
- `WandEnhancer.AutoPatch/TrayAgent.cs` — optional system tray agent.
- `WandEnhancer.AutoPatch/AutoPatchArguments.cs` — CLI argument model.
- `WandEnhancer/View/AutoPatch/AutoPatchSetupVm.cs` — setup view model.
- `WandEnhancer/View/AutoPatch/AutoPatchSetupView.xaml` — setup view.
- `WandEnhancer/Services/ShortcutRegistrar.cs` — replace/restore Wand shortcut.
- `WandEnhancer/Services/ScheduledTaskRegistrar.cs` — create/delete watcher task.
- `WandEnhancer.Core.Tests/WandEnhancer.Core.Tests.csproj` — unit test project.
- `WandEnhancer.Core.Tests/WeModLocatorTests.cs` — path detection tests.
- `WandEnhancer.Core.Tests/ProcessManagerTests.cs` — process termination tests.
- `WandEnhancer.Core.Tests/SettingsStoreTests.cs` — settings I/O tests.

---

## Task 1: Clone the Upstream Repository and Verify Build

**Files:**
- Create: `src/` (clone target)
- Modify: none yet
- Test: `build.ps1` must succeed before changes

**Interfaces:**
- Consumes: none
- Produces: a working copy of the Wand-Enhancer source at `src/`

- [ ] **Step 1: Clone the repository**

```bash
cd "C:/App-Projects/auto-wandehancer"
git clone --depth 1 https://github.com/k1tbyte/Wand-Enhancer.git src
```

- [ ] **Step 2: Verify dependencies**

Ensure the following are installed:
- CMake
- Node.js + pnpm
- Visual Studio 2022 / Build Tools with MSBuild
- .NET Framework 4.8 developer pack

Run:

```powershell
cd src
./build.ps1 -Configuration Debug
```

Expected: build completes successfully with `Build completed successfully (Debug).`

- [ ] **Step 3: Inspect the patch entry point**

Open `src/WandEnhancer/Core/` (or wherever `Enhancer.cs` lives) and identify:
- The class name that performs patching (likely `Enhancer`).
- The method signature (likely `public void Patch()` or `public void Patch(WeModInfo info, Action<string> log, PatchConfig config)`).
- The type of `WeModInfo` and `PatchConfig`/`PatchVectors`.

Record the exact signatures. The rest of the plan assumes:

```csharp
public class Enhancer
{
    public Enhancer(WeModInfo info, ILogger logger, PatchConfig config);
    public void Patch();
}
```

If the actual signature differs, update later tasks accordingly.

- [ ] **Step 4: Commit baseline marker**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add docs/superpowers/plans/2026-07-29-auto-patch-wand-enhancer.md
git commit -m "docs: add auto-patch implementation plan"
```

## Task 2: Create the Shared Class Library `WandEnhancer.Core`

**Files:**
- Create: `src/WandEnhancer.Core/WandEnhancer.Core.csproj`
- Create: `src/WandEnhancer.Core/Properties/AssemblyInfo.cs`
- Test: build the solution

**Interfaces:**
- Consumes: none
- Produces: `WandEnhancer.Core.dll`, referenced by both executables

- [ ] **Step 1: Add the project file**

Create `src/WandEnhancer.Core/WandEnhancer.Core.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{NEW-GUID-HERE}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>WandEnhancer.Core</RootNamespace>
    <AssemblyName>WandEnhancer.Core</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed, processorArchitecture=MSIL">
      <HintPath>..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll</HintPath>
    </Reference>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.Xml" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

Replace `NEW-GUID-HERE` with a fresh GUID generated by `guidgen` or PowerShell:

```powershell
[guid]::NewGuid().ToString().ToUpper()
```

- [ ] **Step 2: Add AssemblyInfo**

Create `src/WandEnhancer.Core/Properties/AssemblyInfo.cs`:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("WandEnhancer.Core")]
[assembly: AssemblyDescription("Shared logic for WandEnhancer")]
[assembly: AssemblyProduct("WandEnhancer")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: ComVisible(false)]
[assembly: Guid("NEW-GUID-HERE")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

- [ ] **Step 3: Add the project to the solution**

Open `src/Wand-Enhancer.sln` and add:

```text
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "WandEnhancer.Core", "WandEnhancer.Core\WandEnhancer.Core.csproj", "{NEW-GUID-HERE}"
EndProject
```

- [ ] **Step 4: Build the empty library**

```powershell
cd src
./build.ps1 -Configuration Debug
```

Expected: build succeeds; `WandEnhancer.Core.dll` is produced in `src/WandEnhancer.Core/bin/Debug/`.

- [ ] **Step 5: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: add WandEnhancer.Core shared library"
```

## Task 3: Extract Path Detection and Validation into `WandEnhancer.Core`

**Files:**
- Create: `src/WandEnhancer.Core/Services/IWeModLocator.cs`
- Create: `src/WandEnhancer.Core/Services/WeModLocator.cs`
- Create: `src/WandEnhancer.Core/Models/WeModInfo.cs` (if not already shared)
- Modify: `src/WandEnhancer/Utils/Extensions.cs` (move detection logic or delegate to Core)
- Test: `src/WandEnhancer.Core.Tests/WeModLocatorTests.cs`

**Interfaces:**
- Consumes: `WeModInfo` model, existing `Extensions.CheckWeModPath` logic
- Produces: `IWeModLocator.LocateAsync()` returning `WeModInfo`

```csharp
public interface IWeModLocator
{
    Task<WeModInfo> LocateAsync(string configuredPath = null);
}
```

- [ ] **Step 1: Define `WeModInfo`**

Create or move `src/WandEnhancer.Core/Models/WeModInfo.cs`:

```csharp
namespace WandEnhancer.Core.Models
{
    public class WeModInfo
    {
        public string BasePath { get; set; }
        public string ExecutablePath { get; set; }
        public string Version { get; set; }
    }
}
```

If an existing `WeModInfo` class is in the WPF project, move it to Core and add `using WandEnhancer.Core.Models;` in the WPF project.

- [ ] **Step 2: Implement `IWeModLocator`**

Create `src/WandEnhancer.Core/Services/IWeModLocator.cs`:

```csharp
using System.Threading.Tasks;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface IWeModLocator
    {
        Task<WeModInfo> LocateAsync(string configuredPath = null);
    }
}
```

Create `src/WandEnhancer.Core/Services/WeModLocator.cs`:

```csharp
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class WeModLocator : IWeModLocator
    {
        private readonly Func<string, bool> _pathValidator;
        private readonly bool _allowManualFallback;

        public WeModLocator(Func<string, bool> pathValidator, bool allowManualFallback = true)
        {
            _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
            _allowManualFallback = allowManualFallback;
        }

        public async Task<WeModInfo> LocateAsync(string configuredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var info = TryBuildInfo(configuredPath);
                if (info != null) return info;
            }

            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Wand"),
            };

            foreach (var candidate in candidates)
            {
                var info = TryBuildInfo(candidate);
                if (info != null) return info;
            }

            var registryPath = FindInRegistry();
            if (!string.IsNullOrWhiteSpace(registryPath))
            {
                var info = TryBuildInfo(registryPath);
                if (info != null) return info;
            }

            if (_allowManualFallback)
            {
                return await PromptUserAsync();
            }

            return null;
        }

        private WeModInfo TryBuildInfo(string basePath)
        {
            if (!_pathValidator(basePath)) return null;
            var exePath = Path.Combine(basePath, "Wand.exe");
            if (!File.Exists(exePath)) return null;

            return new WeModInfo
            {
                BasePath = basePath,
                ExecutablePath = exePath,
                Version = FileVersionInfo.GetVersionInfo(exePath).FileVersion
            };
        }

        private string FindInRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key == null) return null;
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using (var subKey = key.OpenSubKey(subKeyName))
                    {
                        var displayName = subKey?.GetValue("DisplayName") as string;
                        if (displayName == null) continue;
                        if (!displayName.Contains("Wand") && !displayName.Contains("WeMod")) continue;
                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(installLocation)) return installLocation;
                    }
                }
            }
            return null;
        }

        private Task<WeModInfo> PromptUserAsync()
        {
            return Task.Run(() =>
            {
                using (var dialog = new FolderBrowserDialog { Description = "Select your Wand installation folder" })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return TryBuildInfo(dialog.SelectedPath);
                    }
                }
                return null;
            });
        }
    }
}
```

- [ ] **Step 3: Move or delegate validation**

If `Extensions.CheckWeModPath` currently lives in `src/WandEnhancer/Utils/Extensions.cs`, move the validation logic into `src/WandEnhancer.Core/Extensions/PathExtensions.cs`:

```csharp
using System.IO;
using System.Linq;

namespace WandEnhancer.Core.Extensions
{
    public static class PathExtensions
    {
        public static bool CheckWeModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;

            var requiredFiles = new[] { "Wand.exe", "resources", "app.asar" };
            return requiredFiles.All(f =>
                File.Exists(Path.Combine(path, f)) || Directory.Exists(Path.Combine(path, f)));
        }
    }
}
```

Then change the existing WPF `Extensions.CheckWeModPath` to call the Core version:

```csharp
public static bool CheckWeModPath(string path) =>
    WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath(path);
```

- [ ] **Step 4: Reference `WandEnhancer.Core` from the WPF project**

Add to `src/WandEnhancer/WandEnhancer.csproj` inside `<ItemGroup>`:

```xml
<ProjectReference Include="..\WandEnhancer.Core\WandEnhancer.Core.csproj">
  <Project>{NEW-GUID-HERE}</Project>
  <Name>WandEnhancer.Core</Name>
</ProjectReference>
```

- [ ] **Step 5: Add unit tests**

Create `src/WandEnhancer.Core.Tests/WandEnhancer.Core.Tests.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{NEW-TEST-GUID-HERE}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>WandEnhancer.Core.Tests</RootNamespace>
    <AssemblyName>WandEnhancer.Core.Tests</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.VisualStudio.TestTools.TestTestingFramework, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL" />
    <Reference Include="System" />
    <Reference Include="System.Core" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WandEnhancer.Core\WandEnhancer.Core.csproj" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

Note: If MSTest is unavailable, use a lightweight assertion helper instead. The existing repo may not use a test framework. Add a minimal in-project test runner only if needed.

Create `src/WandEnhancer.Core.Tests/WeModLocatorTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestClass]
    public class WeModLocatorTests
    {
        [TestMethod]
        public async Task LocateAsync_WithConfiguredPath_ReturnsInfo()
        {
            var tempDir = CreateFakeWandDir();
            var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
            var info = await locator.LocateAsync(tempDir);
            Assert.IsNotNull(info);
            Assert.AreEqual(tempDir, info.BasePath);
            Directory.Delete(tempDir, recursive: true);
        }

        [TestMethod]
        public async Task LocateAsync_WithInvalidConfiguredPath_FallsBack()
        {
            var tempDir = CreateFakeWandDir();
            var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
            var info = await locator.LocateAsync("C:\\NonExistent\\Wand");
            Assert.IsNotNull(info);
            StringAssert.Contains(info.BasePath, "Wand");
            Directory.Delete(tempDir, recursive: true);
        }

        private string CreateFakeWandDir()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Wand.exe"), "fake");
            Directory.CreateDirectory(Path.Combine(path, "resources"));
            File.WriteAllText(Path.Combine(path, "app.asar"), "fake");
            return path;
        }
    }
}
```

- [ ] **Step 6: Build and run tests**

```powershell
cd src
./build.ps1 -Configuration Debug
```

If a test runner is configured, run:

```powershell
vstest.console.exe "WandEnhancer.Core.Tests/bin/Debug/WandEnhancer.Core.Tests.dll"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: extract path detection into WandEnhancer.Core"
```

## Task 4: Extract Patch Settings Store into `WandEnhancer.Core`

**Files:**
- Create: `src/WandEnhancer.Core/Models/PatchConfig.cs`
- Create: `src/WandEnhancer.Core/Services/ISettingsStore.cs`
- Create: `src/WandEnhancer.Core/Services/SettingsStore.cs`
- Modify: `src/WandEnhancer/Constants.cs` (ensure `AppSettingsFileName` is shared)
- Test: `src/WandEnhancer.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: existing patch options from `MainWindowVm`/popup
- Produces: `ISettingsStore.Load()` / `Save(PatchConfig)`

```csharp
public interface ISettingsStore
{
    PatchConfig Load();
    void Save(PatchConfig config);
}
```

- [ ] **Step 1: Define `PatchConfig`**

Create `src/WandEnhancer.Core/Models/PatchConfig.cs` based on the options currently shown in `PatchVectorsPopup`. If the existing project already has a settings model, move it here and add `using WandEnhancer.Core.Models;` in the WPF project.

Start with this minimal shape and expand to match the actual UI options:

```csharp
using System.Collections.Generic;

namespace WandEnhancer.Core.Models
{
    public class PatchConfig
    {
        public bool UnlockPro { get; set; }
        public bool DisableTelemetry { get; set; }
        public bool EnableRemotePanel { get; set; }
        public bool EnableAiFeatures { get; set; }
        public List<string> RendererScripts { get; set; } = new List<string>();
        public string Theme { get; set; }
        public string WeModPath { get; set; }
    }
}
```

If the main UI uses a different model name (e.g., `PatchVectorsModel`), replace `PatchConfig` with that name and add the same properties.

- [ ] **Step 2: Implement `ISettingsStore`**

Create `src/WandEnhancer.Core/Services/ISettingsStore.cs`:

```csharp
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface ISettingsStore
    {
        PatchConfig Load();
        void Save(PatchConfig config);
    }
}
```

Create `src/WandEnhancer.Core/Services/SettingsStore.cs`:

```csharp
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class SettingsStore : ISettingsStore
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public SettingsStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public PatchConfig Load()
        {
            _lock.EnterReadLock();
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new PatchConfig();
                }
                var json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<PatchConfig>(json) ?? new PatchConfig();
            }
            catch (Exception)
            {
                return new PatchConfig();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Save(PatchConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _lock.EnterWriteLock();
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Replace(tempPath, _filePath, _filePath + ".backup", ignoreSourceMetadata: true);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
```

- [ ] **Step 3: Make the WPF app use `SettingsStore`**

In `MainWindowVm.cs`, replace direct `appsettings.json` reads/writes with `ISettingsStore`. The exact change depends on the current code, but the goal is:

```csharp
private readonly ISettingsStore _settingsStore = new SettingsStore(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 "WandEnhancer", Constants.AppSettingsFileName));
```

When the user changes patch options, call `_settingsStore.Save(config)`.
When the app starts, call `_settingsStore.Load()` to restore options.

- [ ] **Step 4: Add tests**

Create `src/WandEnhancer.Core.Tests/SettingsStoreTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestClass]
    public class SettingsStoreTests
    {
        [TestMethod]
        public void SaveAndLoad_RoundTripsConfig()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            var store = new SettingsStore(path);
            var config = new PatchConfig
            {
                UnlockPro = true,
                WeModPath = "C:\\Wand"
            };
            store.Save(config);
            var loaded = store.Load();
            Assert.IsTrue(loaded.UnlockPro);
            Assert.AreEqual("C:\\Wand", loaded.WeModPath);
            File.Delete(path);
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            var store = new SettingsStore(path);
            var loaded = store.Load();
            Assert.IsFalse(loaded.UnlockPro);
        }
    }
}
```

- [ ] **Step 5: Build and test**

```powershell
cd src
./build.ps1 -Configuration Debug
vstest.console.exe "WandEnhancer.Core.Tests/bin/Debug/WandEnhancer.Core.Tests.dll"
```

Expected: build succeeds and tests pass.

- [ ] **Step 6: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: extract settings store into WandEnhancer.Core"
```

## Task 5: Extract Process Termination and Patch Invocation into `WandEnhancer.Core`

**Files:**
- Create: `src/WandEnhancer.Core/Services/IProcessManager.cs`
- Create: `src/WandEnhancer.Core/Services/ProcessManager.cs`
- Create: `src/WandEnhancer.Core/Services/IPatcher.cs`
- Create: `src/WandEnhancer.Core/Services/Patcher.cs`
- Create: `src/WandEnhancer.Core/Services/ILogger.cs`
- Create: `src/WandEnhancer.Core/Services/FileLogger.cs`
- Modify: `src/WandEnhancer/Core/Enhancer.cs` (or equivalent patch entry point) — move invocation logic
- Test: `src/WandEnhancer.Core.Tests/ProcessManagerTests.cs`

**Interfaces:**
- Consumes: `WeModInfo`, `PatchConfig`, existing `Enhancer` class
- Produces: `IProcessManager.TerminateAllWandProcessesAsync()`, `IPatcher.PatchAsync(WeModInfo, PatchConfig)`

```csharp
public interface IProcessManager
{
    Task TerminateAllWandProcessesAsync(TimeSpan timeout);
}

public interface IPatcher
{
    Task PatchAsync(WeModInfo info, PatchConfig config);
}
```

- [ ] **Step 1: Add `ILogger` and `FileLogger`**

Create `src/WandEnhancer.Core/Services/ILogger.cs`:

```csharp
namespace WandEnhancer.Core.Services
{
    public interface ILogger
    {
        void Info(string message);
        void Error(string message);
    }
}
```

Create `src/WandEnhancer.Core/Services/FileLogger.cs`:

```csharp
using System;
using System.IO;

namespace WandEnhancer.Core.Services
{
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();

        public FileLogger(string logDirectory)
        {
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            Directory.CreateDirectory(_logDirectory);
        }

        public void Info(string message) => Write("INFO", message);
        public void Error(string message) => Write("ERROR", message);

        private void Write(string level, string message)
        {
            var fileName = $"auto-patch-{DateTime.Now:yyyyMMdd}.log";
            var line = $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(Path.Combine(_logDirectory, fileName), line);
            }
        }
    }
}
```

- [ ] **Step 2: Add `IProcessManager` and `ProcessManager`**

Create `src/WandEnhancer.Core/Services/IProcessManager.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace WandEnhancer.Core.Services
{
    public interface IProcessManager
    {
        Task TerminateAllWandProcessesAsync(TimeSpan timeout);
    }
}
```

Create `src/WandEnhancer.Core/Services/ProcessManager.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WandEnhancer.Core.Services
{
    public class ProcessManager : IProcessManager
    {
        private readonly string[] _processNames = { "Wand", "WeMod" };
        private readonly ILogger _logger;

        public ProcessManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TerminateAllWandProcessesAsync(TimeSpan timeout)
        {
            var processes = _processNames
                .SelectMany(name => Process.GetProcessesByName(name))
                .Distinct()
                .ToList();

            if (!processes.Any()) return;

            _logger.Info($"Terminating {processes.Count} Wand process(es).");

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to close main window of process {process.Id}: {ex.Message}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));

            var deadline = DateTime.UtcNow + timeout;
            foreach (var process in processes.ToList())
            {
                try
                {
                    if (!process.HasExited)
                    {
                        if (!process.WaitForExit((int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds)))
                        {
                            _logger.Info($"Force killing process {process.Id}.");
                            process.Kill();
                            process.WaitForExit(2000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to terminate process {process.Id}: {ex.Message}");
                    throw;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }
}
```

- [ ] **Step 3: Add `IPatcher` and `Patcher`**

Create `src/WandEnhancer.Core/Services/IPatcher.cs`:

```csharp
using System.Threading.Tasks;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public interface IPatcher
    {
        Task PatchAsync(WeModInfo info, PatchConfig config);
    }
}
```

Create `src/WandEnhancer.Core/Services/Patcher.cs`:

```csharp
using System;
using System.Threading.Tasks;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class Patcher : IPatcher
    {
        private readonly ILogger _logger;

        public Patcher(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task PatchAsync(WeModInfo info, PatchConfig config)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return Task.Run(() =>
            {
                _logger.Info($"Starting patch for Wand at {info.BasePath}");
                var enhancer = new Enhancer(info, _logger, config);
                enhancer.Patch();
                _logger.Info("Patch completed successfully.");
            });
        }
    }
}
```

If the existing `Enhancer` constructor signature differs, adjust the constructor call to match the actual signature discovered in Task 1.

- [ ] **Step 4: Add process termination tests**

Create `src/WandEnhancer.Core.Tests/ProcessManagerTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestClass]
    public class ProcessManagerTests
    {
        [TestMethod]
        public async Task TerminateAllWandProcessesAsync_KillsDummyWandProcess()
        {
            var logger = new MemoryLogger();
            var manager = new ProcessManager(logger);

            var dummyExe = Path.Combine(Path.GetTempPath(), "Wand.exe");
            File.Copy("cmd.exe", dummyExe, overwrite: true);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = dummyExe,
                Arguments = "/c ping 127.0.0.1 -n 60 >nul",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Assert.IsFalse(process.HasExited);
            await manager.TerminateAllWandProcessesAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(process.HasExited);

            process.Dispose();
            File.Delete(dummyExe);
        }
    }

    public class MemoryLogger : ILogger
    {
        public void Info(string message) { }
        public void Error(string message) { }
    }
}
```

- [ ] **Step 5: Build and test**

```powershell
cd src
./build.ps1 -Configuration Debug
vstest.console.exe "WandEnhancer.Core.Tests/bin/Debug/WandEnhancer.Core.Tests.dll"
```

Expected: build succeeds and tests pass.

- [ ] **Step 6: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: extract process termination and patch invocation into Core"
```

## Task 6: Create `WandEnhancer.AutoPatch.exe`

**Files:**
- Create: `src/WandEnhancer.AutoPatch/WandEnhancer.AutoPatch.csproj`
- Create: `src/WandEnhancer.AutoPatch/Program.cs`
- Create: `src/WandEnhancer.AutoPatch/AutoPatchArguments.cs`
- Create: `src/WandEnhancer.AutoPatch/PatchModeController.cs`
- Create: `src/WandEnhancer.AutoPatch/LaunchModeController.cs`
- Create: `src/WandEnhancer.AutoPatch/WatchModeController.cs`
- Create: `src/WandEnhancer.AutoPatch/ProgressWindow.cs`
- Create: `src/WandEnhancer.AutoPatch/TrayAgent.cs`
- Modify: `src/Wand-Enhancer.sln`
- Test: manual run of `--patch` against a fake Wand dir

**Interfaces:**
- Consumes: `WandEnhancer.Core` services
- Produces: `WandEnhancer.AutoPatch.exe` supporting `--patch`, `--launch`, `--watch`

- [ ] **Step 1: Add the project**

Create `src/WandEnhancer.AutoPatch/WandEnhancer.AutoPatch.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{NEW-AUTOPATCH-GUID-HERE}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>WandEnhancer.AutoPatch</RootNamespace>
    <AssemblyName>WandEnhancer.AutoPatch</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WandEnhancer.Core\WandEnhancer.Core.csproj">
      <Project>{NEW-CORE-GUID-HERE}</Project>
      <Name>WandEnhancer.Core</Name>
    </ProjectReference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

- [ ] **Step 2: Add argument model**

Create `src/WandEnhancer.AutoPatch/AutoPatchArguments.cs`:

```csharp
namespace WandEnhancer.AutoPatch
{
    public class AutoPatchArguments
    {
        public string Mode { get; set; }
        public string WeModPath { get; set; }

        public static AutoPatchArguments Parse(string[] args)
        {
            var result = new AutoPatchArguments();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--patch":
                        result.Mode = "patch";
                        break;
                    case "--launch":
                        result.Mode = "launch";
                        break;
                    case "--watch":
                        result.Mode = "watch";
                        break;
                    default:
                        if (!args[i].StartsWith("--") && string.IsNullOrEmpty(result.WeModPath))
                        {
                            result.WeModPath = args[i];
                        }
                        break;
                }
            }
            return result;
        }
    }
}
```

- [ ] **Step 3: Add progress window**

Create `src/WandEnhancer.AutoPatch/ProgressWindow.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace WandEnhancer.AutoPatch
{
    public class ProgressWindow : Form
    {
        private readonly Label _statusLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _retryButton;
        private readonly Button _openMainButton;

        public ProgressWindow()
        {
            Text = "Wand Enhancer Auto-Patch";
            Size = new Size(400, 160);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _statusLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(360, 20),
                Text = "Preparing..."
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 50),
                Size = new Size(360, 20),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };

            _retryButton = new Button
            {
                Text = "Retry",
                Location = new Point(220, 90),
                Size = new Size(75, 23),
                Visible = false
            };
            _retryButton.Click += (s, e) => RetryRequested?.Invoke(this, EventArgs.Empty);

            _openMainButton = new Button
            {
                Text = "Open WandEnhancer",
                Location = new Point(110, 90),
                Size = new Size(100, 23),
                Visible = false
            };
            _openMainButton.Click += (s, e) => OpenMainRequested?.Invoke(this, EventArgs.Empty);

            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
            Controls.Add(_retryButton);
            Controls.Add(_openMainButton);
        }

        public event EventHandler RetryRequested;
        public event EventHandler OpenMainRequested;

        public void SetStatus(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStatus(text)));
                return;
            }
            _statusLabel.Text = text;
        }

        public void ShowSuccess(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowSuccess(message)));
                return;
            }
            _statusLabel.Text = message;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) => { timer.Stop(); Close(); };
            timer.Start();
        }

        public void ShowFailure(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowFailure(message)));
                return;
            }
            _statusLabel.Text = message;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _retryButton.Visible = true;
            _openMainButton.Visible = true;
        }
    }
}
```

- [ ] **Step 4: Add `PatchModeController`**

Create `src/WandEnhancer.AutoPatch/PatchModeController.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class PatchModeController
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IWeModLocator _locator;
        private readonly IProcessManager _processManager;
        private readonly IPatcher _patcher;
        private readonly ILogger _logger;

        public PatchModeController(
            ISettingsStore settingsStore,
            IWeModLocator locator,
            IProcessManager processManager,
            IPatcher patcher,
            ILogger logger)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _patcher = patcher ?? throw new ArgumentNullException(nameof(patcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RunAsync(string configuredPath, IProgress<string> progress, ProgressWindow window = null)
        {
            try
            {
                progress?.Report("Locating Wand installation...");
                window?.SetStatus("Locating Wand installation...");

                var config = _settingsStore.Load();
                var path = configuredPath ?? config.WeModPath;
                var info = await _locator.LocateAsync(path);

                if (info == null)
                {
                    progress?.Report("Failed to locate Wand installation.");
                    window?.ShowFailure("Could not locate Wand. Open WandEnhancer to set the path.");
                    return false;
                }

                config.WeModPath = info.BasePath;
                _settingsStore.Save(config);

                progress?.Report("Terminating Wand processes...");
                window?.SetStatus("Terminating Wand processes...");
                await _processManager.TerminateAllWandProcessesAsync(TimeSpan.FromSeconds(10));

                progress?.Report("Patching Wand...");
                window?.SetStatus("Patching Wand...");
                await _patcher.PatchAsync(info, config);

                progress?.Report("Patch completed.");
                window?.ShowSuccess("Wand patched successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Auto-patch failed: {ex}");
                progress?.Report($"Patch failed: {ex.Message}");
                window?.ShowFailure($"Patch failed: {ex.Message}");
                return false;
            }
        }
    }
}
```

- [ ] **Step 5: Add `LaunchModeController`**

Create `src/WandEnhancer.AutoPatch/LaunchModeController.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class LaunchModeController
    {
        private readonly PatchModeController _patchController;
        private readonly ILogger _logger;

        public LaunchModeController(PatchModeController patchController, ILogger logger)
        {
            _patchController = patchController ?? throw new ArgumentNullException(nameof(patchController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunAsync(string configuredPath, string[] wandArgs, ProgressWindow window)
        {
            var success = await _patchController.RunAsync(configuredPath, null, window);
            if (!success)
            {
                _logger.Error("Launch aborted because patch failed.");
                return;
            }

            window?.SetStatus("Starting Wand...");
            var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
            var info = await locator.LocateAsync(configuredPath);
            if (info == null)
            {
                window?.ShowFailure("Could not find Wand.exe to launch.");
                return;
            }

            var startInfo = new ProcessStartInfo(info.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = info.BasePath
            };
            if (wandArgs != null && wandArgs.Length > 0)
            {
                startInfo.Arguments = string.Join(" ", wandArgs);
            }
            Process.Start(startInfo);
            window?.Close();
        }
    }
}
```

- [ ] **Step 6: Add `WatchModeController`**

Create `src/WandEnhancer.AutoPatch/WatchModeController.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class WatchModeController
    {
        private readonly PatchModeController _patchController;
        private readonly ILogger _logger;
        private FileSystemWatcher _watcher;
        private DateTime _lastEvent = DateTime.MinValue;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(5);

        public WatchModeController(PatchModeController patchController, ILogger logger)
        {
            _patchController = patchController ?? throw new ArgumentNullException(nameof(patchController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task RunAsync(string configuredPath, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
                var info = await locator.LocateAsync(configuredPath);
                if (info == null)
                {
                    _logger.Error("Watcher could not locate Wand installation.");
                    return;
                }

                _watcher = new FileSystemWatcher(info.BasePath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };

                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnChanged;
                _watcher.EnableRaisingEvents = true;

                _logger.Info($"Watcher started for {info.BasePath}");

                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (TaskCanceledException)
                {
                    // expected
                }
                finally
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
            }, token);
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.UtcNow;
            if (now - _lastEvent < _debounceInterval) return;
            _lastEvent = now;

            _logger.Info($"Detected change: {e.FullPath}");
            await Task.Delay(_debounceInterval);
            await _patchController.RunAsync(null, null, null);
        }
    }
}
```

- [ ] **Step 7: Add `TrayAgent`**

Create `src/WandEnhancer.AutoPatch/TrayAgent.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace WandEnhancer.AutoPatch
{
    public class TrayAgent : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _enabledMenuItem;

        public event EventHandler PatchNowClicked;
        public event EventHandler OpenSettingsClicked;
        public event EventHandler ExitClicked;

        public bool WatcherEnabled
        {
            get => _enabledMenuItem.Checked;
            set => _enabledMenuItem.Checked = value;
        }

        public TrayAgent()
        {
            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "WandEnhancer Auto-Patch",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            _enabledMenuItem = new ToolStripMenuItem("Watcher enabled", null, OnToggleEnabled) { Checked = true };
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add("Patch now", null, (s, e) => PatchNowClicked?.Invoke(this, e));
            menu.Items.Add("Open WandEnhancer", null, (s, e) => OpenSettingsClicked?.Invoke(this, e));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitClicked?.Invoke(this, e));

            _icon.ContextMenuStrip = menu;
        }

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _enabledMenuItem.Checked = !_enabledMenuItem.Checked;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 8: Add `Program.cs`**

Create `src/WandEnhancer.AutoPatch/Program.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var arguments = AutoPatchArguments.Parse(args);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsPath = Path.Combine(appData, "WandEnhancer", "appsettings.json");
            var logDirectory = Path.Combine(appData, "WandEnhancer", "logs");

            var logger = new FileLogger(logDirectory);
            var settingsStore = new SettingsStore(settingsPath);
            var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath);
            var processManager = new ProcessManager(logger);
            var patcher = new Patcher(logger);
            var patchController = new PatchModeController(settingsStore, locator, processManager, patcher, logger);

            if (string.IsNullOrEmpty(arguments.Mode))
            {
                MessageBox.Show("Usage: WandEnhancer.AutoPatch.exe --patch [path] | --launch [path] [wand args] | --watch [path]", "WandEnhancer Auto-Patch");
                return;
            }

            switch (arguments.Mode)
            {
                case "patch":
                    RunPatchMode(patchController, arguments.WeModPath);
                    break;
                case "launch":
                    RunLaunchMode(patchController, arguments.WeModPath, GetWandArgs(args));
                    break;
                case "watch":
                    RunWatchMode(patchController, arguments.WeModPath, logger);
                    break;
            }
        }

        private static void RunPatchMode(PatchModeController controller, string path)
        {
            using (var window = new ProgressWindow())
            {
                var t = controller.RunAsync(path, new Progress<string>(m => window.SetStatus(m)), window);
                window.ShowDialog();
                t.Wait();
            }
        }

        private static void RunLaunchMode(PatchModeController controller, string path, string[] wandArgs)
        {
            using (var window = new ProgressWindow())
            {
                var t = controller.RunAsync(path, new Progress<string>(m => window.SetStatus(m)), window);
                t.ContinueWith(_ =>
                {
                    if (t.Result)
                    {
                        var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
                        var info = locator.LocateAsync(path).Result;
                        if (info != null)
                        {
                            var startInfo = new System.Diagnostics.ProcessStartInfo(info.ExecutablePath)
                            {
                                UseShellExecute = true,
                                WorkingDirectory = info.BasePath,
                                Arguments = wandArgs.Length > 0 ? string.Join(" ", wandArgs) : ""
                            };
                            System.Diagnostics.Process.Start(startInfo);
                        }
                    }
                    window.Invoke(new Action(() => window.Close()));
                }, TaskScheduler.FromCurrentSynchronizationContext());
                window.ShowDialog();
            }
        }

        private static void RunWatchMode(PatchModeController controller, string path, ILogger logger)
        {
            var cts = new CancellationTokenSource();
            var watchController = new WatchModeController(controller, logger);
            var tray = new TrayAgent();

            tray.PatchNowClicked += async (s, e) => await controller.RunAsync(path, null, null);
            tray.OpenSettingsClicked += (s, e) =>
            {
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WandEnhancer.exe");
                if (File.Exists(exePath))
                {
                    System.Diagnostics.Process.Start(exePath);
                }
            };
            tray.ExitClicked += (s, e) =>
            {
                cts.Cancel();
                Application.Exit();
            };

            var task = watchController.RunAsync(path, cts.Token);
            Application.Run(tray);
            cts.Cancel();
            try { task.Wait(TimeSpan.FromSeconds(5)); } catch { }
        }

        private static string[] GetWandArgs(string[] args)
        {
            var list = new List<string>();
            bool foundPath = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--")) continue;
                if (!foundPath)
                {
                    foundPath = true;
                    continue;
                }
                list.Add(args[i]);
            }
            return list.ToArray();
        }
    }
}
```

- [ ] **Step 9: Add project to solution**

Add to `src/Wand-Enhancer.sln`:

```text
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "WandEnhancer.AutoPatch", "WandEnhancer.AutoPatch\WandEnhancer.AutoPatch.csproj", "{NEW-AUTOPATCH-GUID-HERE}"
EndProject
```

- [ ] **Step 10: Build and manual test**

```powershell
cd src
./build.ps1 -Configuration Debug
```

Expected: `WandEnhancer.AutoPatch.exe` is produced in `src/WandEnhancer.AutoPatch/bin/Debug/`.

Manual test:

```powershell
cd "src/WandEnhancer.AutoPatch/bin/Debug"
./WandEnhancer.AutoPatch.exe --patch "C:\Path\To\Wand"
```

Expected: compact progress window appears.

- [ ] **Step 11: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: add WandEnhancer.AutoPatch helper executable"
```

## Task 7: Add Shortcut and Scheduled Task Registration to the Main WPF App

**Files:**
- Create: `src/WandEnhancer/Services/ShortcutRegistrar.cs`
- Create: `src/WandEnhancer/Services/ScheduledTaskRegistrar.cs`
- Create: `src/WandEnhancer/View/AutoPatch/AutoPatchSetupVm.cs`
- Create: `src/WandEnhancer/View/AutoPatch/AutoPatchSetupView.xaml`
- Modify: `src/WandEnhancer/View/MainWindow/MainWindowVm.cs`
- Modify: `src/WandEnhancer/View/MainWindow/MainWindow.xaml`
- Test: manual test of enable/disable auto-patch

**Interfaces:**
- Consumes: `WandEnhancer.Core` services, `ShellLink` COM or `IWshShortcut`
- Produces: `IShortcutRegistrar.ReplaceWandShortcut(...)`, `IScheduledTaskRegistrar.CreateWatcherTask(...)`

- [ ] **Step 1: Add `ShortcutRegistrar`**

Create `src/WandEnhancer/Services/ShortcutRegistrar.cs`:

```csharp
using IWshRuntimeLibrary;
using System;
using System.IO;

namespace WandEnhancer.Services
{
    public interface IShortcutRegistrar
    {
        void ReplaceWandShortcut(string autoPatchExePath, string wandPath);
        void RestoreWandShortcut(string wandPath);
    }

    public class ShortcutRegistrar : IShortcutRegistrar
    {
        private readonly string _startMenuPath;

        public ShortcutRegistrar()
        {
            _startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        }

        public void ReplaceWandShortcut(string autoPatchExePath, string wandPath)
        {
            var shortcutPath = FindWandShortcut();
            if (shortcutPath == null) throw new InvalidOperationException("Could not find Wand shortcut in Start Menu.");

            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            var originalTarget = shortcut.TargetPath;

            File.WriteAllText(shortcutPath + ".original", originalTarget);

            shortcut.TargetPath = autoPatchExePath;
            shortcut.Arguments = $"--launch \"{wandPath}\"";
            shortcut.WorkingDirectory = Path.GetDirectoryName(autoPatchExePath);
            shortcut.Save();
        }

        public void RestoreWandShortcut(string wandPath)
        {
            var shortcutPath = FindWandShortcut();
            if (shortcutPath == null) return;

            var backupPath = shortcutPath + ".original";
            if (!File.Exists(backupPath)) return;

            var originalTarget = File.ReadAllText(backupPath).Trim();
            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = originalTarget;
            shortcut.Arguments = "";
            shortcut.WorkingDirectory = Path.GetDirectoryName(originalTarget);
            shortcut.Save();
            File.Delete(backupPath);
        }

        private string FindWandShortcut()
        {
            foreach (var file in Directory.EnumerateFiles(_startMenuPath, "*.lnk", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.IndexOf("Wand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("WeMod", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return file;
                }
            }
            return null;
        }
    }
}
```

Note: this requires a reference to `IWshRuntimeLibrary`. Add the reference in the WPF project:

```xml
<COMReference Include="IWshRuntimeLibrary">
  <Guid>{F935DC20-1CF0-11D0-ADB9-00C04FD58A0B}</Guid>
  <VersionMajor>1</VersionMajor>
  <VersionMinor>0</VersionMinor>
  <Lcid>0</Lcid>
  <WrapperTool>tlbimp</WrapperTool>
  <Isolated>False</Isolated>
  <EmbedInteropTypes>True</EmbedInteropTypes>
</COMReference>
```

- [ ] **Step 2: Add `ScheduledTaskRegistrar`**

Create `src/WandEnhancer/Services/ScheduledTaskRegistrar.cs`:

```csharp
using Microsoft.Win32.TaskScheduler;
using System;
using System.IO;
using System.Security.Principal;

namespace WandEnhancer.Services
{
    public interface IScheduledTaskRegistrar
    {
        void CreateWatcherTask(string autoPatchExePath, string wandPath);
        void DeleteWatcherTask();
        bool IsWatcherTaskRegistered();
    }

    public class ScheduledTaskRegistrar : IScheduledTaskRegistrar
    {
        private const string TaskName = "WandEnhancerAutoPatchWatcher";

        public void CreateWatcherTask(string autoPatchExePath, string wandPath)
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask(TaskName, false);

                var definition = taskService.NewTask();
                definition.RegistrationInfo.Description = "Watches the Wand installation and re-applies WandEnhancer patches after updates.";
                definition.Principal.RunLevel = TaskRunLevel.Highest;
                definition.Triggers.Add(new LogonTrigger { UserId = WindowsIdentity.GetCurrent().Name });
                definition.Actions.Add(new ExecAction(autoPatchExePath, $"--watch \"{wandPath}\"", Path.GetDirectoryName(autoPatchExePath)));
                definition.Settings.AllowDemandStart = true;
                definition.Settings.StartWhenAvailable = true;

                taskService.RootFolder.RegisterTaskDefinition(TaskName, definition);
            }
        }

        public void DeleteWatcherTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask(TaskName, false);
            }
        }

        public bool IsWatcherTaskRegistered()
        {
            using (var taskService = new TaskService())
            {
                return taskService.RootFolder.Tasks.Exists(TaskName);
            }
        }
    }
}
```

This requires the `Microsoft.Win32.TaskScheduler` NuGet package. Add it to `WandEnhancer.csproj`:

```xml
<PackageReference Include="Microsoft.Win32.TaskScheduler" Version="2.10.1" />
```

If `packages.config` is used, run:

```powershell
nuget install Microsoft.Win32.TaskScheduler -Version 2.10.1 -OutputDirectory packages
```

And add the reference manually.

- [ ] **Step 3: Add setup view model**

Create `src/WandEnhancer/View/AutoPatch/AutoPatchSetupVm.cs`:

```csharp
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;
using WandEnhancer.Services;

namespace WandEnhancer.View.AutoPatch
{
    public class AutoPatchSetupVm : ReactiveObject
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IWeModLocator _locator;
        private readonly IShortcutRegistrar _shortcutRegistrar;
        private readonly IScheduledTaskRegistrar _taskRegistrar;
        private string _statusMessage;
        private bool _isEnabled;

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public ReactiveCommand<Unit, Unit> EnableAutoPatchCommand { get; }
        public ReactiveCommand<Unit, Unit> DisableAutoPatchCommand { get; }
        public ReactiveCommand<Unit, Unit> PickPathCommand { get; }

        public string WeModPath { get; set; }

        public AutoPatchSetupVm(
            ISettingsStore settingsStore,
            IWeModLocator locator,
            IShortcutRegistrar shortcutRegistrar,
            IScheduledTaskRegistrar taskRegistrar)
        {
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _shortcutRegistrar = shortcutRegistrar ?? throw new ArgumentNullException(nameof(shortcutRegistrar));
            _taskRegistrar = taskRegistrar ?? throw new ArgumentNullException(nameof(taskRegistrar));

            var canEnable = this.WhenAnyValue(x => x.WeModPath, path => !string.IsNullOrWhiteSpace(path));
            EnableAutoPatchCommand = ReactiveCommand.CreateFromTask(EnableAsync, canEnable);
            DisableAutoPatchCommand = ReactiveCommand.CreateFromTask(DisableAsync);
            PickPathCommand = ReactiveCommand.Create(PickPath);

            LoadState();
        }

        private void LoadState()
        {
            var config = _settingsStore.Load();
            WeModPath = config.WeModPath;
            IsEnabled = _taskRegistrar.IsWatcherTaskRegistered();
            StatusMessage = IsEnabled ? "Auto-patch is enabled." : "Auto-patch is disabled.";
        }

        private void PickPath()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select Wand installation folder" })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    WeModPath = dialog.SelectedPath;
                    var config = _settingsStore.Load();
                    config.WeModPath = WeModPath;
                    _settingsStore.Save(config);
                    StatusMessage = $"Path set to: {WeModPath}";
                }
            }
        }

        private async Task EnableAsync()
        {
            try
            {
                var info = await _locator.LocateAsync(WeModPath);
                if (info == null)
                {
                    StatusMessage = "Could not locate Wand. Please pick the install path.";
                    return;
                }

                var config = _settingsStore.Load();
                config.WeModPath = info.BasePath;
                _settingsStore.Save(config);

                var autoPatchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WandEnhancer.AutoPatch.exe");
                _shortcutRegistrar.ReplaceWandShortcut(autoPatchPath, info.BasePath);
                _taskRegistrar.CreateWatcherTask(autoPatchPath, info.BasePath);

                IsEnabled = true;
                StatusMessage = "Auto-patch enabled. Wand shortcut and watcher registered.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to enable: {ex.Message}";
            }
        }

        private async Task DisableAsync()
        {
            try
            {
                var config = _settingsStore.Load();
                _shortcutRegistrar.RestoreWandShortcut(config.WeModPath);
                _taskRegistrar.DeleteWatcherTask();
                IsEnabled = false;
                StatusMessage = "Auto-patch disabled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to disable: {ex.Message}";
            }
        }
    }
}
```

- [ ] **Step 4: Add setup view**

Create `src/WandEnhancer/View/AutoPatch/AutoPatchSetupView.xaml`:

```xml
<UserControl x:Class="WandEnhancer.View.AutoPatch.AutoPatchSetupView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <StackPanel Margin="20">
        <TextBlock Text="Auto-Patch Setup" FontSize="20" FontWeight="Bold" Margin="0,0,0,10" />
        <TextBlock Text="{Binding StatusMessage}" Margin="0,0,0,10" TextWrapping="Wrap" />
        <TextBlock Text="{Binding WeModPath, StringFormat='Wand path: {0}'}" Margin="0,0,0,10" />
        <Button Content="Pick Wand Install Path" Command="{Binding PickPathCommand}" Margin="0,0,0,10" />
        <Button Content="Enable Auto-Patch" Command="{Binding EnableAutoPatchCommand}" Margin="0,0,0,10" />
        <Button Content="Disable Auto-Patch" Command="{Binding DisableAutoPatchCommand}" />
    </StackPanel>
</UserControl>
```

Create code-behind `src/WandEnhancer/View/AutoPatch/AutoPatchSetupView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace WandEnhancer.View.AutoPatch
{
    public partial class AutoPatchSetupView : UserControl
    {
        public AutoPatchSetupView()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 5: Wire into main window**

Modify `src/WandEnhancer/View/MainWindow/MainWindow.xaml` to add a menu/button that opens the setup popup. Add near existing controls:

```xml
<Button Content="Auto-Patch Setup" Click="OpenAutoPatchSetupClicked" />
```

Add the handler in `MainWindow.xaml.cs`:

```csharp
private void OpenAutoPatchSetupClicked(object sender, MouseButtonEventArgs e)
{
    var settingsStore = new WandEnhancer.Core.Services.SettingsStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "WandEnhancer", Constants.AppSettingsFileName));
    var locator = new WandEnhancer.Core.Services.WeModLocator(
        WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath);
    var vm = new View.AutoPatch.AutoPatchSetupVm(
        settingsStore,
        locator,
        new Services.ShortcutRegistrar(),
        new Services.ScheduledTaskRegistrar());
    var view = new View.AutoPatch.AutoPatchSetupView { DataContext = vm };
    OpenPopup(view, "Auto-Patch Setup");
}
```

- [ ] **Step 6: Build and manual test**

```powershell
cd src
./build.ps1 -Configuration Debug
```

Run `WandEnhancer.exe`, open Auto-Patch Setup, pick a path, and click **Enable Auto-Patch**. Verify:
- `WandEnhancer.AutoPatch.exe --launch` is set as the Wand shortcut target.
- A scheduled task named `WandEnhancerAutoPatchWatcher` exists.
- Clicking **Disable Auto-Patch** restores the original shortcut and deletes the task.

- [ ] **Step 7: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: add auto-patch setup flow to main WPF app"
```

## Task 8: Update Build Script and Ensure Both Executables Are Packaged Together

**Files:**
- Modify: `src/build.ps1`
- Modify: `src/WandEnhancer/WandEnhancer.csproj` (copy AutoPatch.exe to output)
- Test: clean build produces both EXEs in the same output directory

**Interfaces:**
- Consumes: `WandEnhancer.csproj`, `WandEnhancer.AutoPatch.csproj`
- Produces: combined build output with `WandEnhancer.exe` and `WandEnhancer.AutoPatch.exe`

- [ ] **Step 1: Ensure AutoPatch output is copied next to main EXE**

Add to `src/WandEnhancer/WandEnhancer.csproj` inside a `<Target>`:

```xml
<Target Name="CopyAutoPatch" AfterTargets="Build">
  <PropertyGroup>
    <AutoPatchOutput>..\WandEnhancer.AutoPatch\bin\$(Configuration)\WandEnhancer.AutoPatch.exe</AutoPatchOutput>
  </PropertyGroup>
  <Copy SourceFiles="$(AutoPatchOutput)" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
</Target>
```

- [ ] **Step 2: Update `build.ps1` to build the full solution**

Ensure `build.ps1` builds `Wand-Enhancer.sln`, not just the WPF project. The current script already uses the solution file; verify the MSBuild step is:

```powershell
& $msbuild $solutionFile /p:Configuration=$Configuration /p:Platform="x64" /restore /m
```

If it builds only `WandEnhancer.csproj`, change to the solution file.

- [ ] **Step 3: Verify combined output**

```powershell
cd src
./build.ps1 -Configuration Release
```

Check that `src/WandEnhancer/bin/Release/` contains:
- `WandEnhancer.exe`
- `WandEnhancer.AutoPatch.exe`
- `WandEnhancer.Core.dll`

- [ ] **Step 4: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "build: package WandEnhancer.AutoPatch with main executable"
```

## Task 9: Add Error Handling and Logging Coverage

**Files:**
- Modify: `src/WandEnhancer.AutoPatch/PatchModeController.cs` (already has try/catch)
- Modify: `src/WandEnhancer.AutoPatch/Program.cs`
- Create: `src/WandEnhancer.Core/Services/NotificationService.cs`
- Test: simulate failure cases

**Interfaces:**
- Consumes: `ILogger`
- Produces: user-visible toast notifications on failure

- [ ] **Step 1: Add notification helper**

Create `src/WandEnhancer.Core/Services/NotificationService.cs`:

```csharp
using System.Windows.Forms;

namespace WandEnhancer.Core.Services
{
    public interface INotificationService
    {
        void Show(string title, string message, ToolTipIcon icon);
    }

    public class NotificationService : INotificationService
    {
        public void Show(string title, string message, ToolTipIcon icon)
        {
            using (var iconControl = new NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application,
                BalloonTipTitle = title,
                BalloonTipText = message,
                BalloonTipIcon = icon
            })
            {
                iconControl.ShowBalloonTip(3000);
            }
        }
    }
}
```

- [ ] **Step 2: Use notifications from watcher and patch failure paths**

Update `WatchModeController.OnChanged` to call the notification service after patching:

```csharp
var notification = new NotificationService();
if (await _patchController.RunAsync(null, null, null))
{
    notification.Show("Wand Enhancer", "Wand patched after update.", ToolTipIcon.Info);
}
else
{
    notification.Show("Wand Enhancer", "Auto-patch failed. Open WandEnhancer for details.", ToolTipIcon.Error);
}
```

- [ ] **Step 3: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add .
git commit -m "feat: add tray notifications for auto-patch status"
```

## Task 10: Final Integration Testing and Documentation Update

**Files:**
- Modify: `docs/superpowers/specs/2026-07-29-auto-patch-wand-enhancer-design.md` (mark any changes)
- Modify: `src/README.md` (add auto-patch usage section)
- Test: end-to-end manual test

- [ ] **Step 1: End-to-end manual test**

1. Build Release.
2. Copy `WandEnhancer.exe`, `WandEnhancer.AutoPatch.exe`, and dependencies to a clean folder.
3. Run `WandEnhancer.exe`.
4. Enable Auto-Patch.
5. Click the Wand shortcut — confirm `AutoPatch.exe` launches, patches, then starts Wand.
6. Simulate an update by touching a file in the Wand install directory — confirm the watcher re-patches.
7. Disable Auto-Patch — confirm the original shortcut is restored.

- [ ] **Step 2: Update README**

Add a section to `src/README.md`:

```markdown
## Auto-Patch

Wand-Enhancer can automatically keep Wand patched after updates.

1. Build the project.
2. Run `WandEnhancer.exe`.
3. Click **Auto-Patch Setup** and enable it.
4. The Wand shortcut will launch through `WandEnhancer.AutoPatch.exe`, and a watcher will re-patch after background updates.
```

- [ ] **Step 3: Commit**

```bash
cd "C:/App-Projects/auto-wandehancer/src"
git add README.md
git commit -m "docs: add auto-patch usage instructions"
```

---

## Self-Review

### 1. Spec coverage

| Spec Section | Task(s) |
|---|---|
| Two executables | Task 2 (Core), Task 6 (AutoPatch) |
| Auto-detect path + manual fallback | Task 3 |
| Shared `appsettings.json` | Task 4 |
| Terminate Wand before patch | Task 5 |
| `--patch`, `--launch`, `--watch` modes | Task 6 |
| Replace shortcut + scheduled task | Task 7 |
| Compact progress window | Task 6 |
| Optional tray agent | Task 6, Task 9 |
| Error handling/logging | Task 5, Task 9 |
| Testing | Tasks 3, 4, 5, 10 |

No gaps identified.

### 2. Placeholder scan

Searched for:
- TBD / TODO — none.
- "implement later" / "fill in details" — none.
- Vague requirements like "add validation" — all concrete.
- "Similar to Task N" — none.
- All code blocks contain actual code.

One note: exact signatures of the existing `Enhancer` class must be verified in Task 1 and propagated to Task 5. The plan explicitly instructs this.

### 3. Type consistency

- `IWeModLocator.LocateAsync(string)` returns `Task<WeModInfo>` — consistent across Task 3 and Task 6.
- `ISettingsStore.Load()` returns `PatchConfig`, `Save(PatchConfig)` — consistent across Task 4, Task 6, Task 7.
- `IProcessManager.TerminateAllWandProcessesAsync(TimeSpan)` — consistent across Task 5 and Task 6.
- `IPatcher.PatchAsync(WeModInfo, PatchConfig)` — consistent across Task 5 and Task 6.

No type mismatches found.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-29-auto-patch-wand-enhancer.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

2. **Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints for review.

**Which approach do you want?**
