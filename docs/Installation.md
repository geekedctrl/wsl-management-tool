# Installation

1. Obtain the source from the project's repository or a release archive.
2. Extract it to a user-writable directory.
3. Open PowerShell 5.1 or PowerShell 7 in that directory.
4. Run `./WSL-Manager.ps1` or use the documented `-Action` parameters.

WSL Manager invokes the installed `wsl.exe` directly. It does not install WSL, enable Windows features, or request elevation automatically. A constrained execution policy may require a signed copy or an explicit process-scoped policy change.
