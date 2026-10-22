# Troubleshooting

- **`wsl.exe` not found:** install/enable WSL through Windows settings or Microsoft instructions, then reopen PowerShell. WSL Manager does not modify Windows optional features.
- **No distributions found:** run `wsl --list --verbose` and install/import a distribution if desired.
- **Backup fails or leaves `.partial`:** keep the partial file for review; check free space and WSL error output. Do not assume it is restorable.
- **Restore refuses destination:** choose a new, empty path and a new distribution name. Existing contents are never overwritten.
- **Move unsupported:** the installed help did not advertise `--manage --move`. WSL Manager intentionally does not emulate it.
- **`Access denied`:** run only the specific command under the permissions it requires; WSL Manager never elevates the whole process.
- **PowerShell output looks corrupted:** use a current PowerShell host and retain verbose stderr when reporting a bug. The WSL adapter decodes UTF-16LE output.
- **Imported distro starts as root:** this is the default behavior for imported WSL distributions unless the image provides suitable user configuration.
