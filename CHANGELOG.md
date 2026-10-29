# Changelog

## Unreleased — desktop preview

- Add a native WinUI 3 desktop client with shared PowerShell backend, distribution overview, backup/restore, move/clone, storage, diagnostics, history, and settings pages.
- Add protected system-managed distribution guards in both the desktop client and PowerShell service layer.
- Require a reviewed `-WhatIf` preflight before the desktop client offers disk expansion.
- Add desktop core unit coverage and unpackaged runtime/build documentation.
- Add a self-contained, per-user Setup `.exe` installer build using Inno Setup.
- Add bundled distribution logos and a readable details table with N/A values for unavailable fields.
- Allow installing a catalog image under a distinct WSL registration name when the installed WSL supports `--name`; add per-user default export, backup, and installation folders.
- Add an online WSL catalog dropdown and a confirmed flow to install or import a new distribution through the shared PowerShell backend.
- Keep live data-changing integration flows labeled preview until disposable WSL validation is completed.

## 1.0.0 - 2026-10-22

- Initial implementation of terminal menu and CLI.
- Added distribution discovery and management, backups, restore, clone, native move gating, diagnostics, disk reporting, structured logs and manifests.
- Added mock-adapter Pester unit tests and Windows CI workflow.
- Live destructive integration tests are intentionally omitted.
