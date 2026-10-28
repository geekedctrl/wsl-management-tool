# Architecture

- `WSL-Manager.ps1` owns parameter validation, CLI routing, output formatting, and the terminal menu.
- `src/Core/WSLManager.psm1` owns the process adapter, capability discovery, safety checks, logging, and service operations.
- `src/WSLManager.Desktop` is the unpackaged WinUI 3 client. It collects user input, performs presentation-level preflight, displays contextual confirmations, and calls the same script/module backend through the `WSLManager.Desktop.Core` process bridge. It does not call `wsl.exe` directly.
- `src/WSLManager.Desktop.Core` builds PowerShell arguments as separate process arguments, captures JSON/stdout/stderr/exit status, supports cancellation, stores per-user theme/log settings, parses distribution summaries, sanitizes diagnostics/errors, writes validated redacted diagnostics exports, and provides UI-independent safety/confirmation policies.
- `tests/Unit` injects a mock WSL executor, so unit tests do not invoke the host's WSL installation.
- `tests/WSLManager.Desktop.Core.Tests` tests argument building, process response/error handling, cancellation, settings persistence, confirmation/protected-distribution policies, distribution JSON parsing, diagnostic/error redaction, and diagnostics JSON file output without launching WSL.

The adapter passes argument arrays through Windows command-line quoting and captures stdout/stderr and exit codes. WSL's UTF-16LE console output is decoded before parsing. Distribution parsing uses the stable `--list --verbose` table shape and tolerates non-entry lines. Capability detection checks installed `wsl --help`; documentation and version text are reported separately.

Operation logs and backup history are stored in `%LOCALAPPDATA%\WSLManager`. Logs contain operation metadata and sanitized process error text, not Linux file contents. Registration paths are read only from the current user's WSL registration metadata. The default operation-log retention is 90 days and can be changed with `WSL_MANAGER_LOG_RETENTION_DAYS` (1 to 3650).

The desktop application is unpackaged and uses Windows App SDK 2.5.1. Its runtime prerequisites are documented in [Installation.md](Installation.md). The UI delegates mutations to the PowerShell backend; the backend refuses disruptive management operations against recognized system-managed distributions, and the UI mirrors that policy. Live backup, restore, move, clone, and resize workflows remain unvalidated against disposable WSL installations.
