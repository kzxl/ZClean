# 🌌 CleanTool — Modern System Optimizer & Cleaner

<p align="center">
  <strong>High-performance, modular system & developer workspace cleaner</strong><br/>
  C# WPF Dark UI + CLI Engine • Universe Plugin Architecture v4.0 • Zero-Risk Dry-Run
</p>

---

## 📖 Overview

**CleanTool** is a modern disk maintenance and system cleanup utility engineered for developer workstations and power users. It decouples cleanup rules into autonomous, self-registering plugins while providing a premium Fluent Dark UI and headless CLI capabilities.

| Component | Tech Stack | Role |
| :--- | :--- | :--- |
| **CleanTool.Core** | C# .NET 8 (netstandard / net8.0) | Scan engine, rule registry, safety barrier, file lock probing, disk analyzer |
| **CleanTool.Rules** | C# .NET 8 | Autonomous cleanup plugins (System, Developer, Browsers, Applications) |
| **CleanTool.Cli** | C# .NET 8 Console | Headless command-line runner for automation, CI/CD, and scheduled maintenance |
| **CleanTool.UI** | C# WPF .NET 8-windows | Modern Fluent Dark desktop interface with Live In-Window notifications |
| **CleanTool.Tests** | xUnit .NET 8 | Unit test suite verifying protection guards, exclusions, and rules |

---

## 🛡️ Core Safety Guarantees

1. **Simulation (Dry-Run) by Default**:
   - Both CLI and GUI run in dry-run mode by default, computing re-claimable space and validating paths without deleting any files.
2. **Immutable System Protection**:
   - `SafetyGuard` prevents deletion of critical OS files (`System32`, `SysWOW64`, `pagefile.sys`, `bootmgr`, volume root directories).
3. **File Lock & Active Session Detection**:
   - Probes exclusive write locks. Files actively held by running processes are automatically skipped.
4. **Age Threshold Policy**:
   - By default, files created or modified within the last 24 hours are preserved to protect ongoing tasks and active temporary sessions.

---

## 🛠️ Categories & Rules

### 💻 System
* **User Temporary Files (`sys.temp.user`)**: Cleans `%TEMP%` runtime leftovers.
* **Windows System Temp (`sys.temp.windows`)**: Cleans `C:\Windows\Temp`.
* **Crash & Memory Dumps (`sys.crashdumps`)**: Post-mortem crash dumps (`*.dmp`).
* **Thumbnail Cache (`sys.thumbnails`)**: Cleans `thumbcache_*.db` (opt-in).
* **Windows Error Reports (`sys.wer`)**: Archived telemetry and error logs.
* **Recycle Bin (`sys.recyclebin`)**: Queries and clears Windows Recycle Bin via Shell API.

### 🚀 Developer
* **NuGet Package Cache (`dev.nuget.cache`)**: Global package cache (`~/.nuget/packages`).
* **npm Cache (`dev.npm.cache`)**: Downloaded package tarballs in `%APPDATA%\npm-cache`.
* **Python pip Cache (`dev.pip.cache`)**: Cached wheels in `%LOCALAPPDATA%\pip\cache`.
* **Go Build Cache (`dev.go.cache`)**: Compiled binary objects in `%LOCALAPPDATA%\go-build`.

### 🌐 Browsers
* **Chromium Browsers Cache (`browser.chromium.cache`)**: Network & GPU cache for Chrome, Edge, Brave. Preserves cookies & logins.
* **Firefox Cache (`browser.firefox.cache`)**: Media and script cache (`cache2`).

### 📱 Applications
* **VS Code Cache (`app.vscode.cache`)**: Editor caches, GPUCache, and VSIX downloads.
* **Discord Cache (`app.discord.cache`)**: Cached media, audio streams, and Electron GPU cache.

---

## 💻 CLI Usage

```bash
# List all registered cleanup rules
cleantool list

# Scan system for reclaimable junk (Safe Dry-Run)
cleantool scan

# Scan only developer caches
cleantool scan --category Developer

# Inspect drive storage allocations
cleantool analyze

# Inspect specific directory footprint
cleantool analyze C:\Users

# Clean with explicit execution (Live mode)
cleantool clean --execute
```

---

## 📦 Build & Publish

### Full (Self-Contained Single File)
```bash
dotnet publish src/CleanTool.UI/CleanTool.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/full
```

### Lite (Framework-Dependent Single File)
```bash
dotnet publish src/CleanTool.UI/CleanTool.UI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/lite
```
