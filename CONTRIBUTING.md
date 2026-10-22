# Contributing

Use PowerShell 5.1-compatible syntax unless a newer runtime is explicitly capability-gated. Keep external commands behind the injectable WSL adapter. Unit tests must use the mock executor and must not call destructive WSL commands.

Before submitting changes, run `Invoke-Pester ./tests/Unit`, review the diff for paths, logs, secrets and generated disk files, and document any capability not verified on supported Windows/WSL releases. Changes to destructive behavior require tests that prove `-WhatIf`, confirmation, exact target matching, and protected-distribution behavior.

Issues and pull requests should include Windows build, PowerShell version, WSL version, minimal reproduction, and sanitized error output. Do not attach Linux filesystem content or backup files.
