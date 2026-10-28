# Compatibility and capability report

Local assessment (2026-10-22): Windows 11 build 26200; PowerShell 7.6.7 and Windows PowerShell 5.1.26100.8115; WSL 2.6.3.0 and kernel 6.6.87.2; Git 2.56.0; Pester 3.4.0 installed. The environment was queried read-only. Distribution registration, VHDX allocated size, Linux filesystem usage and running state were discoverable. Installed WSL help advertised native move and resize commands.

The official [Microsoft WSL command reference](https://learn.microsoft.com/windows/wsl/basic-commands) documents `--export` TAR and `--vhd`, `--import`, `--import-in-place`, `--terminate`, and destructive `--unregister`. Microsoft's [disk management guide](https://learn.microsoft.com/windows/wsl/disk-space) documents `--manage <distro> --resize` and directs users to shut down all WSL instances first; this application requires all distributions stopped and does not perform global shutdown. Expansion also requires the optional read-only `Get-VHD` cmdlet for safe capacity preflight. Native `--manage --move` is enabled only if installed help advertises it; it also requires a verified NTFS volume and sufficient free space. Global and per-distribution configuration scopes are described in Microsoft's [WSL configuration guide](https://learn.microsoft.com/windows/wsl/wsl-config).

PowerShell 5.1 and PowerShell 7 syntax, unit tests and CLI list output passed locally. Pester 3.4.0 is available locally; CI uses current Pester on Windows. VHDX export/restore, conversion, move, and disk expansion have not been run against live distributions. The PowerShell terminal menu itself has not been manually exercised in this session.

The native WinUI 3 client targets .NET 10 and Windows App SDK 2.5.1, with a Windows 10 build 17763 minimum target. It builds and launches on the local Windows 11 build 26200 environment. Manual UI checks covered the distribution list/details, navigation, diagnostics redaction, live System/Light/Dark theme persistence, and refusal of Terminate for a recognized protected distribution. A disk expansion preview safely failed because the current user could not inspect the VHD with `Get-VHD`; no resize was attempted. The app is unpackaged and requires the .NET 10 Desktop Runtime and Windows App SDK runtime on target machines. Windows 10 runtime behavior has not been tested here.

## Desktop verification record (2026-10-22)

- `dotnet build WSLManager.Desktop.sln -p:Platform=x64`: passed, 0 warnings and 0 errors.
- `dotnet test tests/WSLManager.Desktop.Core.Tests/WSLManager.Desktop.Core.Tests.csproj`: passed, 37 tests, including empty/malformed distribution responses, error redaction, diagnostics availability messages, and valid redacted diagnostics file output.
- `Invoke-Pester ./tests/Unit/WSLManager.Tests.ps1`: passed, 17 tests under Pester 6.2.0/PowerShell 7 and Pester 3.4.0/Windows PowerShell 5.1. Coverage includes missing `wsl.exe`, retention cleanup with mocked WSL, and isolated temporary logs; Windows PowerShell 5.1 CLI diagnostics JSON also parsed successfully.
- PSScriptAnalyzer 1.25.0: scanned the entry point, backend module, and unit tests; no analyzer errors. It reported 21 warnings and 54 informational style findings. Warnings are intentional TUI host output, established plural service/test names, mocked executor parameters, and exact-name confirmation for unregister; the nullable comparison was corrected.
- GUI window launched at 1360×900. Distribution list, details, diagnostics, storage and settings/theme controls were visible through the accessibility tree. Theme was changed to Dark and Light, then returned to the pre-existing System preference.
- Folder and file-open pickers opened and were canceled. An initial file-save picker check was canceled; a later Save As run completed, and the resulting JSON parsed with profile paths redacted.
- At 960×700 logical minimum size on a 200% display, Diagnostics and operation-history controls remained reachable. Empty history was clearly reported. Core export writing is unit-tested on a temp file; the native save picker was also completed and the resulting JSON/redaction verified.
- Dark theme preference survived a full app restart. The original System preference and 90-day log retention were restored. System-follow behavior across a Windows theme change remains untested.
- Log retention was set to 30 in the UI, saved, verified after relaunch, then restored to the original 90-day value.
- Keyboard smoke: Tab visibly focused Settings navigation, Enter opened the page, and Tab order reached theme, retention, and Save Settings with visible focus outlines. Full keyboard/accessibility review remains open.
- Controlled absent-WSL and no-registered-distro UI states were checked with an isolated PATH and a temporary mock `wsl.exe` that refused all non-read-only commands. Both diagnostics messages explained the condition; existing WSL distributions were not involved.
- An ordinary Debian Terminate confirmation was opened and canceled. The UI stated that no backend command was run; final read-only WSL listing showed both existing distributions still running as WSL 2.
- Exception messages displayed by the desktop now pass through the shared redactor for profile paths and secret-like assignments. Distribution JSON parsing is in the testable Core layer; latest built-app smoke rendered the installed list successfully.
- Clicking Terminate for `docker-desktop` displayed the protected-operation refusal; no confirmation dialog appeared and no WSL action ran.
- Live backup, restore, move, clone, conversion and resize were not executed. No disposable test distribution was available, and the registered distributions are user/system-managed data; live integration scenarios are skipped by design.
- No registered distribution, Windows WSL setting, or global WSL configuration was modified by these checks. The UI created/updated only its per-user preference file, which was left at its original System/90-day values.

## Current development-preview validation (2026-10-28)

- Desktop solution build with user-local .NET SDK 10.0.402: passed with 0 warnings and 0 errors.
- Desktop Core xUnit suite: 39 passed, 0 failed, 0 skipped.
- PowerShell unit suite: 17 passed under PowerShell 7.6.7 / Pester 6.2.0 and Windows PowerShell 5.1 / Pester 3.4.0.
- The PowerShell terminal menu was smoke-tested for distribution listing, invalid input recovery, navigation, and clean exit.
- The maximized desktop UI was visually reviewed on the local Windows 11 machine, including the responsive distribution, backup/restore, move/clone, storage, and diagnostics layouts. Windows 10 runtime behavior remains unverified.
- Live backup, restore, move, clone, conversion, disk-expansion, and failure-recovery integration tests remain skipped because no positively identified disposable distro was provisioned. No existing distribution was modified.
