# Command reference

Entry point: `WSL-Manager.ps1`. Run without `-Action` to open the menu.

Common parameters: `-Action`, `-Distro`, `-Destination`, `-Source`, `-Format Tar|Vhdx`, `-NewName`, `-OutputFormat Human|Json`, `-Version 1|2`, `-WhatIf`, `-Confirm`, `-Force`, `-Verbose`, `-Help`.

| Action | Required arguments | Behavior |
|---|---|---|
| `List` | none | List registered distributions and default/protected status. |
| `Info` | `-Distro` | Show registration details, discoverable install/VHDX paths and size, and read-only Linux filesystem data for a distro that is already running. |
| `Start` | `-Distro` | Starts selected distro with a no-op command. |
| `Terminate` | `-Distro` | Terminates only the selected distro. |
| `SetDefault` | `-Distro` | Changes the default distro. |
| `Convert` | `-Distro -Version` | Converts WSL version; conversion can take time and change disk format. |
| `Unregister` | `-Distro` | Destructive. Requires confirmation and typing the exact name. Protected system distributions are refused. |
| `Backup` | `-Distro -Destination [-Format]` | Creates collision-resistant TAR or WSL 2 VHDX export, manifest, and SHA-256. Destination directory must already exist. |
| `Restore` | `-Source -Distro -Destination` | Registers as a new distro. Destination/name conflicts are refused. TAR and VHDX use WSL import with the selected installation directory. |
| `Move` | `-Distro -Destination` | Uses native `--manage --move` only when advertised. No fallback. |
| `Clone` | `-Distro -NewName -Destination` | Export/import clone; source is retained. |
| `Diagnose` | optional `-OutputFormat Json` | Reports WSL status/version, detected distributions, Windows/PowerShell versions, volumes and `.wslconfig`. |
| `Disk` | optional `-Distro` | Reports host volume space, discoverable VHDX allocated size, and Linux filesystem usage for already-running user distros. These are different measurements. |
| `Resize` | `-Distro -Size` | Capability-gated WSL 2 expansion with VHDX/volume preflight; requires `Get-VHD` and all distributions stopped. WSL Manager never runs global `wsl --shutdown`. |

Examples:

```powershell
./WSL-Manager.ps1 -Action List -OutputFormat Json
./WSL-Manager.ps1 -Action Backup -Distro Ubuntu -Destination 'D:\WSL Backups' -Format Vhdx -WhatIf
./WSL-Manager.ps1 -Action Backup -Distro Ubuntu,Debian -Destination 'D:\WSL Backups' -Format Tar
./WSL-Manager.ps1 -Action Restore -Source 'D:\WSL Backups\Ubuntu.vhdx' -Distro Ubuntu-Copy -Destination 'D:\WSL\Ubuntu-Copy'
./WSL-Manager.ps1 -Action Convert -Distro Debian -Version 2 -Confirm
./WSL-Manager.ps1 -Action Disk -Distro Debian -OutputFormat Json
./WSL-Manager.ps1 -Action Resize -Distro Debian -Size 128GB -WhatIf
./WSL-Manager.ps1 -Action Unregister -Distro Ubuntu
```

Exit code is 0 on success and 1 on validation or operation failure. JSON is returned on stdout when requested; errors are written to stderr. PowerShell's common `-WhatIf` and `-Confirm` parameters apply to mutation actions. `-Force` never bypasses conflict, protected-distro, or destructive-action checks.

This version provides read-only `.wslconfig` inspection. It does not edit `.wslconfig` or `/etc/wsl.conf`.
