# FIRST Package Manager

A modernized, cross-platform package manager for FRC and FTC development tools. Replaces the monolithic WPILib installer with a small (~37 MB) application that gives teams granular control over what they download and install.

## Quick Start

```bash
# List available packages (fetches from online registry)
frc list --available

# Install the FRC Java starter kit (JDK, VS Code, WPILib, tools)
frc install --bundle frc-java-starter-2026

# Install a single package
frc install wpilib.jdk

# Check what's installed
frc list --installed

# CSA quick setup
frc install --csa
```

Or launch the GUI:
```bash
FrcToolsuite.Gui.exe
```

## Features

- **Package registry** with 18+ packages from WPILib, CTRE, REV, PathPlanner, PhotonVision, and community vendors
- **Bundle system** for one-click starter kit installation (FRC Java, FRC C++, CSA USB Toolkit)
- **Offline/USB mode** for competitions and schools without internet
- **Cross-platform** targeting: prepare a USB on Windows for Mac/Linux deployment
- **Team profiles** for consistent setups across all team laptops
- **Health diagnostics** to verify toolchain integrity
- **Legacy detection** recognizes existing WPILib installer installations
- **Differential updates** download only changed packages
- **GUI and CLI** interfaces

## Installation

### Download

Download the latest release from [GitHub Releases](https://github.com/jasondaming/first-package-manager/releases):
- `FrcToolsuite.Gui.exe` (37 MB) - Graphical application
- `FrcToolsuite.Cli.exe` (33 MB) - Command-line tool (rename to `frc.exe`)

### Build from Source

Requires .NET 10 SDK.

```bash
git clone https://github.com/jasondaming/first-package-manager.git
cd first-package-manager
dotnet build FrcToolsuite.sln

# Run the GUI
dotnet run --project src/FrcToolsuite.Gui

# Run the CLI
dotnet run --project src/FrcToolsuite.Cli -- --help

# Publish standalone executables
dotnet publish src/FrcToolsuite.Gui -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/gui
dotnet publish src/FrcToolsuite.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/cli
```

## CLI Reference

### Global Options

All commands accept these options:

| Option | Description |
|--------|-------------|
| `-p, --program <frc\|ftc>` | Competition program (default: frc) |
| `-y, --year <year>` | Season year (e.g. 2026) |
| `--json` | Output results as JSON |
| `-v, --verbose` | Enable verbose logging |
| `--offline` | Use cached data only (no network) |

### `frc install [package-or-bundle]`

Install a package or bundle.

```bash
# Install a single package
frc install wpilib.jdk

# Install a bundle (starter kit)
frc install --bundle frc-java-starter-2026

# Skip confirmation
frc install wpilib.advantagescope --yes

# CSA quick setup (installs csa-usb-toolkit-2026 bundle)
frc install --csa

# Volunteer/team quick setup (installs frc-java-starter-2026 bundle)
frc install --volunteer
```

| Option | Description |
|--------|-------------|
| `--bundle` | Treat argument as a bundle name |
| `-Y, --yes` | Skip confirmation prompt |
| `--csa` | Shortcut: install CSA USB Toolkit |
| `--volunteer` | Shortcut: install team starter kit |

### `frc update [package-id]`

Update installed packages.

```bash
# Interactive: shows available updates, lets you choose
frc update

# Update a specific package
frc update wpilib.advantagescope

# Update everything
frc update --all
```

### `frc uninstall <package-id>`

Remove an installed package.

```bash
frc uninstall wpilib.shuffleboard
```

Checks reverse dependencies before removing. If another package depends on the one being removed, the uninstall is blocked with a message explaining which package requires it.

### `frc list`

List packages.

```bash
# Show installed packages (default)
frc list --installed

# Show all available packages from registry
frc list --available

# Show packages with available updates
frc list --updates
```

### `frc search <query>`

Search the package registry.

```bash
frc search phoenix
frc search dashboard
frc search rev
```

### `frc health [--fix]`

Check installation health and integrity.

```bash
# Run diagnostics
frc health

# Auto-fix what's possible
frc health --fix

# Check a specific package
frc health --package wpilib.jdk
```

Checks:
- Install directory exists and is writable
- Package manifests are valid
- Required dependencies are present

### `frc sync-usb <drive-path>`

Prepare a USB drive for offline installation at competitions or schools without internet.

```bash
# Sync a bundle to USB (downloads all platforms by default)
frc sync-usb E: --bundle frc-java-starter-2026

# Target specific platforms (saves bandwidth)
frc sync-usb E: --bundle frc-java-starter-2026 --platform windows-x64,macos-arm64

# Sync currently installed packages
frc sync-usb E:
```

| Option | Description |
|--------|-------------|
| `--bundle <name>` | Bundle to sync |
| `--platform <list>` | Comma-separated target platforms: `windows-x64`, `macos-x64`, `macos-arm64`, `linux-x64`, `linux-arm64` |

The USB drive will contain:
- Registry snapshot
- Package artifacts for selected platforms
- Portable installer executable

### `frc profile <export|import|apply>`

Manage team installation profiles for consistent setups across laptops.

```bash
# Export current installation as a profile
frc profile export team-profile.json

# View a profile's contents
frc profile import team-profile.json

# Apply a profile (install all packages listed)
frc profile apply team-profile.json
```

Profile JSON format:
```json
{
  "schemaVersion": 1,
  "profileName": "Team 6391 - Programming Laptop",
  "teamNumber": 6391,
  "competition": "Frc",
  "season": 2026,
  "packages": [
    { "id": "wpilib.jdk", "version": "17.0.16+8" },
    { "id": "wpilib.vscode", "version": "1.105.1" },
    { "id": "ctre.phoenix6", "version": "26.1.0" }
  ]
}
```

### `frc config <set|get|list>`

Manage application settings.

```bash
# Show all settings
frc config list

# Get a specific setting
frc config get installDirectory

# Set a value
frc config set installDirectory "D:\frc"
frc config set autoCheckUpdates false
frc config set proxyUrl "http://proxy.school.edu:8080"
```

Available settings:

| Key | Default | Description |
|-----|---------|-------------|
| `installDirectory` | `C:\frc` (Win) / `~/frc` (Mac/Linux) | Where packages are installed |
| `cacheDirectory` | `~/.frctoolsuite/cache` | Download cache location |
| `proxyUrl` | *(empty)* | HTTP proxy for school networks |
| `autoCheckUpdates` | `true` | Check for updates on startup |
| `theme` | `system` | UI theme |
| `selectedProgram` | `Frc` | Default competition program |
| `selectedSeason` | `2026` | Default season year |

### `frc self-update`

Check for and install updates to the package manager itself.

## Unattended / Scripted Installation

For IT administrators deploying to lab machines via SCCM, Intune, Group Policy, or imaging scripts.

### Silent mode

```bash
# Install with zero console output, log to file
frc install --bundle frc-java-starter-2026 --yes --silent --log C:\frc-install.log

# Exit codes: 0=success, 1=failure
echo %ERRORLEVEL%
```

### Config file-based install

Create `install-config.json`:
```json
{
  "bundle": "frc-java-starter-2026"
}
```

Or specify individual packages:
```json
{
  "packages": ["wpilib.jdk", "wpilib.vscode", "wpilib.gradlerio", "ctre.phoenix6"]
}
```

Run unattended:
```bash
frc --config install-config.json --silent --log install.log
```

### Global options for scripting

| Option | Description |
|--------|-------------|
| `--silent` | Suppress all console output |
| `--log <path>` | Write timestamped log to file |
| `--config <path>` | Install from JSON config file (implies --yes) |
| `--yes` / `-Y` | Skip confirmation prompts |
| `--offline` | Use cached data only (no network) |

### Example: Imaging day script

```batch
@echo off
REM Deploy FRC tools to lab machines
frc install --bundle frc-java-starter-2026 --yes --silent --log C:\frc-install-%COMPUTERNAME%.log
if %ERRORLEVEL% NEQ 0 (
    echo FAILED - check log at C:\frc-install-%COMPUTERNAME%.log
    exit /b 1
)
echo SUCCESS
```

## Available Packages (2026 Season)

### WPILib Core
| Package | Description | Size |
|---------|-------------|------|
| `wpilib.jdk` | JDK 17 (Adoptium Temurin) | ~191 MB |
| `wpilib.vscode` | VS Code + WPILib Extension | ~130 MB |
| `wpilib.gradlerio` | GradleRIO / WPILib libraries | ~50 MB |
| `wpilib.advantagescope` | AdvantageScope v26.0.0 | ~95 MB |
| `wpilib.elastic` | Elastic Dashboard v2026.1.1 | ~85 MB |

### WPILib Tools (individually installable)
| Package | Description | Size |
|---------|-------------|------|
| `wpilib.glass` | Glass telemetry viewer | ~45 MB |
| `wpilib.sysid` | SysId system identification | ~40 MB |
| `wpilib.shuffleboard` | Shuffleboard dashboard | ~110 MB |
| `wpilib.smartdashboard` | SmartDashboard | ~30 MB |
| `wpilib.outlineviewer` | OutlineViewer (NetworkTables) | ~35 MB |
| `wpilib.datalogtool` | DataLogTool | ~35 MB |
| `wpilib.robobuilder` | RoboBuilder | ~50 MB |

### Vendor Libraries
| Package | Description | Size |
|---------|-------------|------|
| `ctre.phoenix6` | CTRE Phoenix 6 | ~150 MB |
| `rev.revlib` | REVLib | ~85 MB |
| `pathplanner.pathplannerlib` | PathPlannerLib | ~20 MB |
| `photonvision.photonlib` | PhotonLib | ~45 MB |

### Community
| Package | Description | Size |
|---------|-------------|------|
| `community.yagsl` | YAGSL swerve library | ~15 MB |
| `community.advantagekit` | AdvantageKit logging framework | ~25 MB |

## Bundles

| Bundle | Contents | Audience |
|--------|----------|----------|
| `frc-java-starter-2026` | JDK, VS Code, GradleRIO, tools, AdvantageScope, vendor libs | Teams (Java) |
| `frc-cpp-starter-2026` | JDK, VS Code, GradleRIO, tools, AdvantageScope | Teams (C++) |
| `csa-usb-toolkit-2026` | JDK, Glass, SysId, AdvantageScope | CSAs at events |
| `wpilib-tools-2026` | All WPILib tools individually | Anyone |

## Package Registry

Package manifests are hosted in [vendor-json-repo](https://github.com/jasondaming/vendor-json-repo). Vendors can submit new packages via pull request.

Registry structure:
```
vendor-json-repo/
  installer-index.json          # Registry index (fetched on startup)
  packages/                     # Package manifests
    wpilib/jdk-2026.json
    ctre/phoenix6-2026.json
    ...
  bundles/                      # Bundle definitions
    frc-java-starter-2026.json
    ...
  2026/                         # Traditional vendordep JSONs
    Phoenix6-26.1.0.json
    REVLib-2026.0.1.json
    ...
```

### Adding a Package

1. Fork `vendor-json-repo`
2. Create a package manifest in `packages/<publisher>/<name>-<season>.json`
3. Follow the [package manifest schema](registry/schemas/package-manifest.v1.schema.json)
4. Submit a pull request

## Architecture

```
FrcToolsuite.Core          - Business logic (no UI dependency)
FrcToolsuite.Gui           - Avalonia desktop GUI
FrcToolsuite.Cli           - Command-line interface
FrcToolsuite.Platform.*    - OS-specific implementations (Windows/macOS/Linux)
```

See [CLAUDE.md](CLAUDE.md) for build commands and development details.

## Platform Support

| Platform | Status |
|----------|--------|
| Windows x64 | Full support (registry, shortcuts, PATH) |
| macOS x64 | Full support (symlinks, zshrc, osascript) |
| macOS ARM64 | Full support |
| Linux x64 | Full support (.desktop files, shell profiles) |
| Linux ARM64 | Full support |

## License

Apache License 2.0 — see [LICENSE](LICENSE)
