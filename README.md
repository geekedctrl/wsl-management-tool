# WSL Manager

> **Development preview — not production-validated.** Version 1.0.0 has been checked with automated unit tests and local read-only/CLI validation. Live WSL backup, restore, move, and clone integration workflows have not been completed. Treat data-changing features as preview software and maintain independent backups.

WSL Manager includes a PowerShell 5.1+ command-line/terminal menu and a native WinUI 3 desktop client for inspecting and managing Windows Subsystem for Linux distributions. Version 1.0.0 focuses on explicit operations, refusal to overwrite, and clear reporting when WSL does not advertise a capability.

## Supported environment

- The PowerShell CLI is intended for Windows 10/11 with a supported WSL installation. It was locally validated on Windows 11 build 26200, WSL 2.6.3.0, PowerShell 7.6.7, and Windows PowerShell 5.1.26100.8115.
- The desktop preview targets Windows 10 build 17763 or later through the Windows App SDK. .NET 10 support on Windows 10 is limited to supported LTSC/Enterprise editions; Windows 10 runtime behavior has not been tested. The desktop app was only run on Windows 11 in the current validation environment. See Microsoft's [.NET on Windows support table](https://learn.microsoft.com/dotnet/core/install/windows) and [Windows App SDK support matrix](https://learn.microsoft.com/windows/apps/windows-app-sdk/support).
- Normal use has no third-party runtime dependencies. Pester is used for development tests.
- WSL 1 and WSL 2 can be listed, started, terminated, and converted subject to WSL support. VHDX export is WSL 2 only.

## Install and start

Download or clone the repository, then run in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./WSL-Manager.ps1
```

No administrator session is required for normal operations. Windows execution policy is changed only for the current PowerShell process by the example above.

### WinUI desktop preview

The native desktop client is development/preview software and has not been validated with live data-changing workflows. Its navigation covers distribution management, backup and restore, move and clone, storage, diagnostics, operation history, and settings. It adapts its card layout to the window width and uses the same PowerShell services as the CLI.

The unpackaged app requires the .NET 10 Desktop Runtime and Windows App SDK 2.5.1 runtime. PowerShell 7 is preferred; Windows PowerShell 5.1 is used as a fallback. WSL itself is optional for launching the app, but distribution features require a working WSL installation. The app does not install runtime prerequisites, enable Windows features, or elevate itself.

**Preview limitations:** live backup/export, restore, move, clone, conversion, and disk-expansion integration workflows have not been completed. Do not treat the app as production-validated or use it as the only protection for important data. Existing distributions are never automatically overwritten; destructive or disruptive operations require explicit confirmation and protected/system-managed distributions are refused. See [Safety](docs/Safety.md) and [Compatibility](docs/Compatibility.md) before use.

To build and run from a source checkout with the .NET 10 SDK:

```powershell
dotnet build WSLManager.Desktop.sln -p:Platform=x64
dotnet run --project src/WSLManager.Desktop/WSLManager.Desktop.csproj -p:Platform=x64
```

Install the .NET 10 Desktop Runtime and Windows App SDK 2.5.1 runtime before running the unpackaged build on another machine. The app and bundled backend are copied to the build output; no MSI/MSIX package is produced by these commands. See [docs/Installation.md](docs/Installation.md) for details.

## Quick start

```powershell
./WSL-Manager.ps1 -Action List
./WSL-Manager.ps1 -Action Info -Distro Debian
./WSL-Manager.ps1 -Action Diagnose -OutputFormat Json
./WSL-Manager.ps1 -Action Backup -Distro Debian -Destination 'D:\Backups' -Format Tar
./WSL-Manager.ps1 -Action Backup -Distro Debian,Ubuntu -Destination 'D:\Backups' -Format Tar
./WSL-Manager.ps1 -Action Restore -Source 'D:\Backups\Debian_20261022_120000_ab12cd34.tar' -Distro Debian-Restored -Destination 'D:\WSL\Debian-Restored'
./WSL-Manager.ps1 -Action Clone -Distro Debian -NewName Debian-Test -Destination 'D:\WSL\Debian-Test'
./WSL-Manager.ps1 -Action Move -Distro Debian -Destination 'D:\WSL\Debian'
./WSL-Manager.ps1 -Action Disk -Distro Debian -OutputFormat Json
./WSL-Manager.ps1 -Action Resize -Distro Debian -Size 128GB -WhatIf
```

Use `-WhatIf` to preview supported modifications. Use `-Confirm` to request PowerShell confirmation. `-Force` is accepted for script compatibility and deliberately never bypasses safeguards. Unregister requires confirmation and exact name typing.

To rename a distribution safely, clone it under the new name, verify the clone, switch your work to it, then explicitly unregister the old distribution as a separate operation if desired.

## Menus and commands

Run without `-Action` for the numbered terminal menu. The complete parameter reference, caveats, and exit codes are in [docs/Commands.md](docs/Commands.md). Implementation boundaries are documented in [docs/Architecture.md](docs/Architecture.md), [docs/Safety.md](docs/Safety.md), [docs/Compatibility.md](docs/Compatibility.md), and [docs/Troubleshooting.md](docs/Troubleshooting.md).

The menu is keyboard-only and uses text labels. Some operations ask for confirmation or a distribution name. Ctrl+C exits through PowerShell's normal cancellation behavior.

```text
WSL MANAGER v1.0
----------------
Name           Version State   IsDefault IsProtected
Debian         2       Running True      False
docker-desktop 2       Running False     True

1. Distribution Manager
2. Backup & Restore
3. Move & Clone
4. Disk Management
5. WSL Configuration
6. Diagnostics & Reports
7. Settings & Logs
0. Exit
Select:
```

## Safety

Unregister permanently deletes a distribution. Backup exports can interrupt consistency-sensitive workloads; WSL Manager does not stop a running distribution automatically before export. Backups record process completion, file checks, and SHA-256, but do not claim restorable status until a test restoration succeeds. See [docs/Safety.md](docs/Safety.md).

The application does not infer installation paths from registry assumptions, does not fall back from native move to destructive migration, and does not run `wsl --shutdown`.

## Development

```powershell
Install-Module Pester -Scope CurrentUser
Invoke-Pester ./tests/Unit
```

For the desktop core unit tests, build and test the solution with the .NET 10 SDK:

```powershell
dotnet build WSLManager.Desktop.sln -p:Platform=x64
dotnet test tests/WSLManager.Desktop.Core.Tests/WSLManager.Desktop.Core.Tests.csproj
```

GitHub Actions runs the PowerShell unit suite and builds/tests the desktop solution on Windows.

See [CONTRIBUTING.md](CONTRIBUTING.md). Integration test policy and current skip rationale are in [tests/Integration/README.md](tests/Integration/README.md).

## License

MIT. See [LICENSE](LICENSE).
