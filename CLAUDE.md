# CLAUDE.md - FrcToolsuite (FIRST Package Manager)

## Build Commands

```bash
# Restore and build the entire solution
dotnet build FrcToolsuite.slnx

# Build a specific project
dotnet build src/FrcToolsuite.Core/FrcToolsuite.Core.csproj
dotnet build src/FrcToolsuite.Gui/FrcToolsuite.Gui.csproj
dotnet build src/FrcToolsuite.Cli/FrcToolsuite.Cli.csproj

# Run tests
dotnet test tests/FrcToolsuite.Core.Tests/FrcToolsuite.Core.Tests.csproj

# Run a single test by name
dotnet test tests/FrcToolsuite.Core.Tests --filter "FullyQualifiedName~PackageVersionTests.Parse_ValidVersionStrings"

# Run the GUI
dotnet run --project src/FrcToolsuite.Gui/FrcToolsuite.Gui.csproj

# Run the CLI
dotnet run --project src/FrcToolsuite.Cli/FrcToolsuite.Cli.csproj -- --help

# Run the test harness
dotnet run --project tests/FrcToolsuite.TestHarness/FrcToolsuite.TestHarness.csproj -- list
dotnet run --project tests/FrcToolsuite.TestHarness/FrcToolsuite.TestHarness.csproj -- screenshot Home
dotnet run --project tests/FrcToolsuite.TestHarness/FrcToolsuite.TestHarness.csproj -- state Home

# Check formatting (must pass clean before committing)
dotnet format FrcToolsuite.slnx --verify-no-changes --no-restore

# Publish self-contained single-file binaries
dotnet publish src/FrcToolsuite.Gui -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o publish/gui-win-x64
dotnet publish src/FrcToolsuite.Gui -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/gui-osx-arm64
dotnet publish src/FrcToolsuite.Gui -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/gui-linux-x64
dotnet publish src/FrcToolsuite.Cli -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o publish/cli-win-x64
dotnet publish src/FrcToolsuite.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/cli-osx-arm64
dotnet publish src/FrcToolsuite.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/cli-linux-x64
```

## CI / Release

- **CI** (`.github/workflows/ci.yml`): runs on every push/PR to master. Builds and tests on Windows, Ubuntu, macOS. Runs `dotnet format --verify-no-changes` on Ubuntu. Captures UI screenshots on Ubuntu.
- **Release** (`.github/workflows/release.yml`): triggers on `v*.*.*` tag push. Builds self-contained binaries for win-x64, osx-x64, osx-arm64, linux-x64, linux-arm64 and creates a GitHub Release. Use `workflow_dispatch` to do a dry-run build without publishing.

To publish a release: `git tag v1.0.0 && git push origin v1.0.0`

## Architecture Overview

FrcToolsuite is a cross-platform package manager for FIRST Robotics (FRC/FTC) tooling. It manages installation, updating, and configuration of development tools like WPILib, vendor libraries, dashboards, and IDEs.

### Solution Structure

```
FrcToolsuite.slnx
  src/
    FrcToolsuite.Core          - Shared models, interfaces, version logic (no UI dependency)
    FrcToolsuite.Gui           - Avalonia desktop GUI (MVVM with CommunityToolkit.Mvvm)
    FrcToolsuite.Cli           - Command-line interface (System.CommandLine)
    FrcToolsuite.Platform.Windows  - Windows-specific service implementations
    FrcToolsuite.Platform.macOS    - macOS-specific service implementations
    FrcToolsuite.Platform.Linux    - Linux-specific service implementations
  tests/
    FrcToolsuite.Core.Tests    - xUnit tests for Core logic
    FrcToolsuite.TestHarness   - Avalonia headless app for screenshots and state export
  registry/                    - JSON registry data (packages, bundles, schemas)
```

### Key Patterns

- **.NET 10** target framework across all projects (set in Directory.Build.props)
- **Allman braces**, 4-space indentation
- **Nullable** reference types enabled, **TreatWarningsAsErrors** enabled
- **MVVM** in GUI: ViewModels implement `IStateExportable` for testability
- **PackageVersion** class: semver-like parsing with comparison operators and range checking
- **Registry models**: JSON-serializable with `System.Text.Json` attributes (`[JsonPropertyName]`)
- **Platform abstraction**: Core defines interfaces, Platform.* projects provide implementations via DI
- **Cross-platform GUI**: `ServiceConfiguration.cs` selects the correct `IPlatformService` at runtime via `RuntimeInformation.IsOSPlatform`. GUI references all three Platform projects; CLI does the same.

### Core Domain Types

- `PackageVersion` - Version parsing, comparison, range satisfaction
- `PackageManifest` - Full package metadata with artifacts and install info
- `RegistryIndex` - Top-level registry with seasons, publishers, package summaries
- `BundleDefinition` - Curated package collections (starter kits, etc.)
- `TeamProfile` - Exportable team installation configuration
- `IPackageManager` - Install/update/uninstall orchestration interface
- `IRegistryClient` - Registry fetch/search interface

### CLI Commands

`frc install|update|uninstall|list|search|health|sync-usb|profile|config|self-update`

Global flags: `--program`, `--year`, `--json`, `--verbose`, `--offline`
