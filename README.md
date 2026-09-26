<div align="center">

![logo](assets/icon.svg)

# auto-wandehancer

Pre-built installer with automatic updates — no manual Actions workflow needed.

</div>

## What is this?

**auto-wandehancer** is a pre-built, self-updating distribution of
[WandEnhancer](https://github.com/k1tbyte/Wand-Enhancer). Upstream publishes no
executables — you fork it and run its build workflow yourself. This project does
that part for you:

- **Pre-built installer** — download `AutoWandEnhancerSetup.exe` directly from [Releases](https://github.com/barracudaXT/auto-wandehancer/releases/latest) instead of building it yourself.
- **Automatic updates** — the tray watcher checks for new releases every 6 hours and offers one-click silent updates.
- **Automated builds** — a scheduled workflow checks upstream every 6 hours and
  publishes a new release whenever this repository's version has not been
  released yet, merging upstream first with this repository's files winning.

Everything upstream does — patching, the auto-patch watcher, the remote web
panel, custom scripts — is included unchanged.

> **Versioning.** This project versions its own releases, independently of
> upstream. `v2.1.2.0` packages this repository's sources; upstream's changes up
> to its own 2.1.0.0 are included and listed below it in the changelog. Each
> release names the commit it was built from.

## Installation

1. Download **AutoWandEnhancerSetup.exe** from the [latest release](https://github.com/barracudaXT/auto-wandehancer/releases/latest).
2. Run the installer — it auto-detects your Wand/WeMod folder.
3. Accept the UAC prompt once.

The installer sets up everything: the main app, the auto-patch watcher (system tray), the Wand shortcut replacement, and the scheduled task.

> **Note:** The installer is unsigned, so Windows SmartScreen may warn you. This is expected for self-built patching tools. The build supports Authenticode signing via `scripts/sign-artifacts.ps1` (see Building from Source) — a certificate issued by a trusted CA is what removes the warning; a self-signed one does not.

## Updating

Updates are handled automatically:

- The system tray watcher checks for new releases in the background.
- When an update is available, a balloon notification appears and the tray menu changes to **Update available: vX.X.X**.
- Click it to download and install the update silently — no manual steps needed.

You can also check manually: right-click the tray icon → **Check for updates**.

## Features

All features from the upstream project are included:

- Local environment configuration management
- Automated compatibility adjustments for new client versions
- Advanced layout and theme customization (client-side only)
- AI Features
- Remote web panel (control from your phone)
- Automatic re-patching after Wand updates
- Custom JavaScript injection

See the [upstream README](https://github.com/k1tbyte/Wand-Enhancer#readme) for full feature documentation, remote web panel setup, custom scripts guide, and screenshots.

## Auto-Patch

The auto-patch system runs as a lightweight tray application with three modes:

| Mode | What it does |
|------|-------------|
| `--watch` | Monitors the Wand install directory and re-patches after updates. Runs at logon via scheduled task. |
| `--launch` | Patches Wand then launches it. Replaces the Wand shortcut. |
| `--patch` | One-shot patch and exit. |

### Disabling

- **Via the installer:** Apps → Installed apps → **auto-wandehancer** → Uninstall.
- **Via the app:** Open Auto-patch setup (shield icon) → Disable.

## Building from Source

### Requirements

- Windows 10/11
- Visual Studio 2019 or later with MSBuild, or Build Tools for Visual Studio
- .NET Framework 4.8 targeting pack
- Node.js 22.19 or later and pnpm
- Inno Setup 6

### Build

```
.\build.ps1
```

Runs the web panel build, restores and builds the solution, runs the patch-locator
and fork test suites, and packages `dist\AutoWandEnhancerSetup.exe`.

Authenticode signing is performed by the release workflow
(`.github/workflows/build-release.yml`), which signs the executables before the
installer is built and then signs the installer itself. Locally, sign artifacts
with `scripts/sign-artifacts.ps1`.

## License

Apache-2.0 — see [LICENSE](LICENSE.md).

---

> **Disclaimer:** This is a third-party enhancement tool for local interoperability and educational purposes. It does not distribute proprietary code or bypass server-side validations. All modifications are performed locally.
