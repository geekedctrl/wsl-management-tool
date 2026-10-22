# Architecture

- `WSL-Manager.ps1` owns parameter validation, CLI routing, output formatting, and the terminal menu.
- `src/Core/WSLManager.psm1` owns the process adapter, capability discovery, safety checks, logging, and service operations.
- `tests/Unit` injects a mock WSL executor, so unit tests do not invoke the host's WSL installation.

The adapter passes argument arrays through Windows command-line quoting and captures stdout/stderr and exit codes. WSL's UTF-16LE console output is decoded before parsing. Distribution parsing uses the stable `--list --verbose` table shape and tolerates non-entry lines. Capability detection checks installed `wsl --help`; documentation and version text are reported separately.

Operation logs and backup history are stored in `%LOCALAPPDATA%\WSLManager`. Logs contain operation metadata and sanitized process error text, not Linux file contents. Registration paths are read only from the current user's WSL registration metadata. The default operation-log retention is 90 days and can be changed with `WSL_MANAGER_LOG_RETENTION_DAYS` (1 to 3650).
