# 🌌 ZeroClean — Modern System Optimizer & Cleaner

<p align="center">
  <a href="https://github.com/kzxl/ZeroClean"><img src="https://img.shields.io/badge/Type-Desktop%20Application%20%26%20CLI-007ACC?style=flat-square&logo=windows" alt="Type: Desktop App" /></a>
  <a href="https://github.com/kzxl/ZeroClean"><img src="https://img.shields.io/badge/Ecosystem-Zero%20Universe-8A2BE2?style=flat-square" alt="Ecosystem: Zero Universe" /></a>
  <a href="https://github.com/kzxl/ZeroClean"><img src="https://img.shields.io/badge/Platform-Windows%20x64-brightgreen?style=flat-square" alt="Platform: Windows x64" /></a>
  <a href="https://github.com/kzxl/ZeroClean"><img src="https://img.shields.io/badge/Distribution-Standalone%20Single--File-2ea44f?style=flat-square" alt="Distribution: Standalone Single-File" /></a>
</p>

<p align="center">
  <strong>High-performance, modular system & developer workspace cleaner</strong><br/>
  C# WPF Dark UI + CLI Engine • Universe Plugin Architecture v4.0 • Zero-Risk Dry-Run
</p>

---


## 📖 Overview

**ZeroClean** is a modern disk maintenance and system cleanup utility engineered for developer workstations and power users. Part of the sovereign **ZeroUniverse** application suite, it decouples cleanup rules into autonomous, self-registering plugins while providing a premium Fluent Dark UI and headless CLI capabilities.

| Component | Tech Stack | Role |
| :--- | :--- | :--- |
| **ZeroClean.Core** | C# .NET 8 (netstandard / net8.0) | Scan engine, rule registry, safety barrier, file lock probing, disk analyzer |
| **ZeroClean.Rules** | C# .NET 8 | Autonomous cleanup plugins (System, Developer, Browsers, Applications) |
| **ZeroClean.Cli** | C# .NET 8 Console | Headless command-line runner for automation, CI/CD, and scheduled maintenance |
| **ZeroClean.UI** | C# WPF .NET 8-windows | Modern Fluent Dark desktop interface with Live In-Window notifications |
| **ZeroClean.Tests** | xUnit .NET 8 | Unit test suite verifying protection guards, exclusions, and rules |

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
zeroclean list

# Scan system for reclaimable junk (Safe Dry-Run)
zeroclean scan

# Scan only developer caches
zeroclean scan --category Developer

# Inspect drive storage allocations
zeroclean analyze

# Inspect specific directory footprint
zeroclean analyze C:\Users

# Clean with explicit execution (Live mode)
zeroclean clean --execute
```

---

## 📦 Build & Publish

### Full (Self-Contained Single File)
```bash
dotnet publish src/ZeroClean.UI/ZeroClean.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/full
```

### Lite (Framework-Dependent Single File)
```bash
dotnet publish src/ZeroClean.UI/ZeroClean.UI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/lite
```
