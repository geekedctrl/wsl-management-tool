# WSL Manager

WSL Manager is a PowerShell 5.1+ command-line and terminal menu for inspecting and safely managing Windows Subsystem for Linux distributions. Version 1.0.0 focuses on explicit operations, refusal to overwrite, and clear reporting when WSL does not advertise a capability.

## Supported environment

- Windows 10/11 with a supported WSL installation; locally validated on Windows 11 build 26200, WSL 2.6.3.0, PowerShell 7.6.7, and Windows PowerShell 5.1.26100.8115.
- Normal use has no third-party runtime dependencies. Pester is used for development tests.
- WSL 1 and WSL 2 can be listed, started, terminated, and converted subject to WSL support. VHDX export is WSL 2 only.

## Install and start

Download or clone the repository, then run in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./WSL-Manager.ps1
```

No administrator session is required for normal operations. Windows execution policy is changed only for the current PowerShell process by the example above.

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

See [CONTRIBUTING.md](CONTRIBUTING.md). Integration test policy and current skip rationale are in [tests/Integration/README.md](tests/Integration/README.md).

## License

MIT. See [LICENSE](LICENSE).
