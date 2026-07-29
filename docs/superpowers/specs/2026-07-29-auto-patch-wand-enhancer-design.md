# Auto-Patch Wand-Enhancer Design

**Date:** 2026-07-29
**Project:** auto-wandehancer (Wand-Enhancer fork)
**Goal:** Make Wand remain patched after every Wand update with minimal user interaction.

---

## 1. Goal and Scope

Add an automatic patching subsystem to Wand-Enhancer so that:

- Wand is patched automatically after Wand updates itself.
- Every Wand launch goes through the patcher first.
- Running Wand processes are terminated before patching.
- The same patch settings used in the main UI are reused for automatic patches.
- User interaction is minimized: auto-detect the install path once, with a one-time manual fallback.

Out of scope:

- Changing what the patch unlocks.
- Adding new patch vectors or renderer scripts.
- Persisting Wand account state.

---

## 2. Architecture

The solution introduces one new executable and three integration points.

### New executable

`WandEnhancer.AutoPatch.exe` — a lightweight console/WinForms helper built from a new project in the solution. It supports three modes:

| Mode | Flag | Purpose |
|------|------|---------|
| Patch | `--patch` | Kill Wand, apply patch, exit. Used by the watcher. |
| Launch | `--launch` | Kill Wand, apply patch, start Wand.exe, exit. Used by the replaced shortcut. |
| Watch | `--watch` | Start a file watcher on the Wand install directory; run `--patch` when files change. |

### Integration points

1. **`WandEnhancer.Core` (new shared class library)**  
   Extracts the existing patch logic, path detection, process management, and settings storage so both executables use the same code.

2. **`appsettings.json`**  
   The existing settings file becomes the single source of truth for patch options. Both the main app and the helper read and write it safely.

3. **Setup registrar in the main app**  
   A new flow inside `WandEnhancer.exe` enables/disables auto-patching by:
   - replacing the Wand shortcut target with `WandEnhancer.AutoPatch.exe --launch`,
   - creating a Windows scheduled task that runs `WandEnhancer.AutoPatch.exe --watch` at user logon with admin rights.

### Architecture diagram

```
┌─────────────────────┐     ┌─────────────────────┐
│  WandEnhancer.exe   │     │  AutoPatch.exe      │
│  main WPF app       │────▶│  --patch / --launch │
│  setup + manual UI  │     │  --watch            │
└─────────────────────┘     └──────────┬──────────┘
                                       │
        ┌──────────────────────────────┘
        │ reads/writes
        ▼
┌─────────────────────┐     ┌─────────────────────┐
│  appsettings.json   │◄────│  WandEnhancer.Core  │
│  patch settings     │     │  shared logic       │
└─────────────────────┘     └─────────────────────┘
                                       │
                                       ▼
                              ┌─────────────────────┐
                              │  Wand install dir   │
                              │  kill / patch / run │
                              └─────────────────────┘
```

---

## 3. Components

### 3.1 `WandEnhancer.Core`

A new .NET class library referenced by both executables.

- **`PatchConfig`**  
  Settings model that mirrors the options currently selected in the main UI (patch vectors, renderer scripts, remote web panel, etc.).

- **`WeModLocator`**  
  Auto-detects the Wand installation directory:
  1. `%LocalAppData%\Programs\Wand` and similar common paths.
  2. Registry keys under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall` for `Wand` and `WeMod`.
  3. User-provided path from `appsettings.json`.
  4. Falls back to a one-time folder picker if detection fails.
  Validation uses the existing `Extensions.CheckWeModPath` logic.

- **`ProcessManager`**  
  Finds all processes named `Wand.exe` or matching Wand child processes, sends a graceful close message, waits up to 10 seconds, then force-kills any survivors.

- **`Patcher`**  
  Wraps the existing `Enhancer.Patch(...)` call so it can be invoked headlessly. Accepts a `PatchConfig` and an `ILogger`.

- **`SettingsStore`**  
  Thread-safe read/write of `appsettings.json` using a file lock or atomic rename to prevent corruption when both executables access it.

- **`ILogger` / `FileLogger`**  
  Writes logs to `%LocalAppData%\WandEnhancer\logs\auto-patch-yyyyMMdd.log`.

### 3.2 `WandEnhancer.AutoPatch.exe`

A new console/WinForms project. It has no full WPF UI; it shows a compact progress window only when patching.

- **`Program`**  
  Parses command-line arguments and dispatches to the correct mode.

- **`PatchModeController`**  
  Loads settings, locates Wand, kills Wand processes, runs `Patcher`, reports success/failure, and exits.

- **`LaunchModeController`**  
  Same as `PatchModeController`, then starts the real `Wand.exe` with the original shortcut arguments.

- **`WatchModeController`**  
  Uses `FileSystemWatcher` on the Wand install directory. It debounces rapid file events (e.g., 5-second quiet period), then runs `PatchModeController`.

- **`ProgressWindow`**  
  A small WinForms window with a status label and progress bar. Auto-closes on success; stays open on failure with **Retry** and **Open WandEnhancer** buttons.

- **`TrayAgent` (optional, enabled by default)**  
  Shows a system tray icon while `--watch` is running. Provides: status tooltip, manual patch now, open settings, disable watcher, exit.

### 3.3 `WandEnhancer.exe` changes

A new **Auto-Patch Setup** page/flow is added to the existing main window:

- Detect or confirm Wand install path.
- Display current auto-patch status (enabled/disabled).
- **Enable Auto-Patch** button:
  - Validates path.
  - Replaces the Wand shortcut.
  - Creates the scheduled task (requests admin once).
  - Runs an initial `--patch` and reports the result.
- **Disable Auto-Patch** button:
  - Restores the original Wand shortcut.
  - Deletes the scheduled task.

---

## 4. Data Flow

### First-time setup

1. User opens `WandEnhancer.exe`.
2. Clicks **Enable Auto-Patch**.
3. `WeModLocator` tries auto-detect.
   - If detection fails, a one-time folder picker appears.
   - If the user cancels, setup aborts.
4. Valid path is saved to `appsettings.json`.
5. The app registers:
   - Replaced Wand shortcut target → `WandEnhancer.AutoPatch.exe --launch "<path>"`.
   - Scheduled task → `WandEnhancer.AutoPatch.exe --watch "<path>"` at user logon, with admin rights.
6. Runs an initial `--patch` to confirm everything works.

### Normal Wand launch

1. User clicks the Wand shortcut.
2. `WandEnhancer.AutoPatch.exe --launch` starts.
3. Compact progress window appears.
4. Helper loads `PatchConfig` from `appsettings.json`.
5. Helper locates Wand, kills any running Wand processes.
6. Helper applies the patch.
7. On success, helper starts the real `Wand.exe` and exits.

### Wand updates in the background

1. Wand updater writes new files to the install directory.
2. `--watch` mode detects the change via `FileSystemWatcher`.
3. Debounce timer waits 5 seconds for the update to settle.
4. Helper runs the same patch flow as `--patch`.
5. A tray notification/toast reports success or failure.

### Settings change

1. User opens `WandEnhancer.exe` and changes patch options.
2. New settings are saved to `appsettings.json`.
3. The next `--patch` or `--launch` automatically uses the updated settings.

---

## 5. Error Handling

| Failure | Behavior |
|---|---|
| Wand path not found | Log error; show toast; open main `WandEnhancer.exe` so the user can fix the path. |
| Wand processes cannot be terminated | Abort patch; show error dialog; do not start Wand. |
| Patch fails | Show compact error dialog with **Open WandEnhancer** and **Retry** buttons; log full details. |
| `appsettings.json` missing or corrupt | Fall back to safe defaults; notify user in the main UI. |
| Scheduled task or shortcut registration fails | Show setup error in main UI; instruct user to run as administrator. |
| Three consecutive auto-patch failures | Disable the watcher; notify user to open the main UI. |

All error messages are concise and actionable. Full stack traces and details go to `%LocalAppData%\WandEnhancer\logs`.

---

## 6. Security and Permissions

- Administrator elevation is required **once** during setup to create the scheduled task and replace the shortcut.
- The watcher scheduled task runs elevated so it can terminate Wand processes and modify the Wand install directory without repeated UAC prompts.
- `WandEnhancer.AutoPatch.exe` does not open network connections or communicate with remote services.
- All operations stay local to the user's machine.

---

## 7. Testing Plan

| Test | Method |
|---|---|
| Path auto-detection | Unit tests for `WeModLocator` against mocked registry and filesystem. |
| Process termination | Integration test that spawns dummy `Wand.exe` processes and verifies graceful then force kill. |
| Patch reuse | Compare output of `Patcher` against the existing manual `Enhancer.Patch` flow. |
| `--launch` mode | Replace a test shortcut, run it, confirm the target executable is launched after patching. |
| `--watch` mode | Copy files into a temp Wand directory; confirm patch triggers after debounce. |
| Settings round-trip | Write settings in main UI, read them in AutoPatch, assert equality. |
| Error paths | Corrupt settings, missing path, locked process, inaccessible directory. |

---

## 8. Decisions and Trade-offs

- **Two executables vs. one:** Two executables were chosen because the WPF app is too heavy for silent background patches. A lightweight helper starts faster and is easier to schedule.
- **File watcher vs. launcher-only:** A watcher catches background Wand updates; the launcher shortcut catches every user-initiated launch. Both are needed for full coverage.
- **Compact progress window vs. silent:** A compact window gives the user confidence that patching happened, without the overhead of the full WPF UI.
- **Admin once vs. per-patch:** Elevation once at install time avoids annoying UAC prompts every time Wand updates.

---

## 9. Open Questions Resolved During Design

| Question | Decision |
|---|---|
| How is Wand path found? | Auto-detect first, one-time manual fallback. |
| How are updates detected? | `FileSystemWatcher` plus launcher pre-check. |
| Persistent background process? | Optional tray watcher; can be disabled. |
| User experience during auto-patch? | Compact progress window. |
| Settings source? | Shared `appsettings.json`, same as main UI. |
| Running Wand during update? | Terminate all Wand processes before patching. |
| Admin rights? | Elevated once during setup. |
| How is Wand launched? | Existing shortcut target replaced with the launcher helper. |
