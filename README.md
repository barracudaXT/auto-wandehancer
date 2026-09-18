<div align="center">

![logo](assets/icon.svg)

# WandEnhancer (Auto-Build Fork)

Pre-built installer with automatic updates — no manual Actions workflow needed.

</div>

## What is this?

This fork of [Wand-Enhancer](https://github.com/k1tbyte/Wand-Enhancer) adds:

- **Pre-built installer** — download `WandEnhancerSetup.exe` directly from [Releases](https://github.com/barracudaXT/auto-wandehancer/releases/latest) instead of running GitHub Actions yourself.
- **Automatic updates** — the tray watcher checks for new releases every 6 hours and offers one-click silent updates.
- **Automated CI/CD** — a GitHub Actions workflow polls the upstream repo every 6 hours, syncs changes, builds the installer, and publishes a new release automatically.

All upstream features (patching, auto-patch watcher, remote web panel, custom scripts) work the same as in the original project.

> **Upstream version.** This fork builds upstream **2.1.0.0** directly, from
> upstream's own sources at the repository root, and adds the pre-built installer, the
> auto-patch watcher, the updater with download verification, and code signing.


## Installation

1. Download **WandEnhancerSetup.exe** from the [latest release](https://github.com/barracudaXT/auto-wandehancer/releases/latest).
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

- **Via the installer:** Apps → Installed apps → WandEnhancer → Uninstall.
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
and fork test suites, and packages `dist\WandEnhancerSetup.exe`. Add
`-SignPfxPath <certificate.pfx> -SignPfxPassword <password>` to Authenticode-sign
the artifacts.

## License

Apache-2.0 — see [LICENSE](LICENSE.md).

---

> **Disclaimer:** This is a third-party enhancement tool for local interoperability and educational purposes. It does not distribute proprietary code or bypass server-side validations. All modifications are performed locally.
