# Safety model

- Existing distributions are inspected read-only unless the user requests a specific operation.
- Operations are scoped to the exact, case-sensitive registered name.
- Protected container distributions cannot be unregistered or cloned.
- Unregister uses `ShouldProcess`, an additional confirmation, and exact-name typing. `-Force` cannot bypass these checks.
- Restore refuses existing names and destination directories. Backup names are unique and exports stage to `.partial` before finalization.
- Backup manifests distinguish command completion and output/hash checks from restoration; `TestRestoreSucceeded` stays false.
- No selected distro is automatically terminated for backup; no global WSL shutdown is used.
- Move requires an advertised native command. No export/unregister/import fallback is attempted.
- Failed restores preserve destination contents for recovery. Failed clones preserve the temporary TAR on failure.
- Read-only environment assessment is safe against user data. Integration tests never operate on registered user distributions.

Review the target name and paths before approving any mutating command. An export of an active system may not provide application-level consistency.
