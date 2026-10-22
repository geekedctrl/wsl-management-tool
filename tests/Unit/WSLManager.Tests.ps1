Describe 'WSL Manager adapter and discovery' {
    if ((Get-Module Pester).Version.Major -ge 5) {
        BeforeAll {
            $modulePath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'src/Core/WSLManager.psm1'
            Import-Module $modulePath -Force
            function Test-Throws([scriptblock]$Command) { try { & $Command; return $false } catch { return $true } }
            function Assert-Equal($Actual,$Expected,[string]$Message) { if($Actual -ne $Expected){throw ("{0}: expected '{1}', got '{2}'" -f $Message,$Expected,$Actual)} }
        }
    } else {
        $modulePath = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'src/Core/WSLManager.psm1'
        Import-Module $modulePath -Force
        function Test-Throws([scriptblock]$Command) { try { & $Command; return $false } catch { return $true } }
        function Assert-Equal($Actual,$Expected,[string]$Message) { if($Actual -ne $Expected){throw ("{0}: expected '{1}', got '{2}'" -f $Message,$Expected,$Actual)} }
    }

    BeforeEach {
        Set-WslCommandExecutor {
            param($arguments,$allowFailure)
            $line = $arguments -join ' '
            if($line -eq '--list --verbose') { return [pscustomobject]@{ExitCode=0;StdOut="  NAME                   STATE           VERSION`n* Debian                 Running         2`n  Ubuntu-24.04           Stopped         2`n  Legacy                 Stopped         1`n  docker-desktop         Running         2";StdErr=''} }
            if($line -eq '--version') { return [pscustomobject]@{ExitCode=0;StdOut='WSL version: 2.6.3.0';StdErr=''} }
            if($line -eq '--help') { return [pscustomobject]@{ExitCode=0;StdOut='--vhd --import-in-place --manage --resize';StdErr=''} }
            if($line -eq '--status') { return [pscustomobject]@{ExitCode=0;StdOut='Default Distribution: Debian';StdErr=''} }
            return [pscustomobject]@{ExitCode=0;StdOut='';StdErr=''}
        }
    }

    It 'parses one and multiple distributions, versions, default and protected state' {
        $items=@(Get-WslDistributions)
        Assert-Equal $items.Count 4 'Distribution count'
        Assert-Equal ($items | Where-Object Name -eq 'Debian').IsDefault $true 'Default flag'
        Assert-Equal ($items | Where-Object Name -eq 'Legacy').Version 1 'WSL 1 version'
        Assert-Equal ($items | Where-Object Name -eq 'docker-desktop').IsProtected $true 'Protected flag'
    }

    It 'reports capabilities from advertised command help' {
        $c=Get-WslCapabilities
        Assert-Equal $c.SupportsVhdExport $true 'VHD export capability'
        Assert-Equal $c.SupportsManage $true 'Manage capability'
        Assert-Equal $c.SupportsImportInPlace $true 'Import-in-place capability'
    }

    It 'rejects unknown names and does not fuzzy-match' {
        Assert-Equal (Test-Throws { Get-WslDistro 'debian' }) $true 'Exact distro matching'
    }

    It 'rejects VHDX backup from WSL 1' {
        Assert-Equal (Test-Throws { Backup-WslDistribution -Name Legacy -Destination $TestDrive -Format Vhdx -WhatIf }) $true 'WSL 1 cannot export VHDX'
    }

    It 'refuses restore into an existing path before registration' {
        $existing=Join-Path $TestDrive 'already-there'; New-Item -ItemType Directory $existing | Out-Null
        $tar=Join-Path $TestDrive 'sample.tar'; Set-Content $tar 'fixture'
        Assert-Equal (Test-Throws { Restore-WslBackup -Source $tar -Name NewDistro -Destination $existing -WhatIf }) $true 'Restore destination conflict'
    }

    It 'refuses native move when command capability is absent' {
        Set-WslCommandExecutor { param($a,$f) if(($a -join ' ') -eq '--list --verbose'){return [pscustomobject]@{ExitCode=0;StdOut='* Debian Running 2';StdErr=''}} if(($a -join ' ') -eq '--version'){return [pscustomobject]@{ExitCode=0;StdOut='2.0';StdErr=''}} if(($a -join ' ') -eq '--help'){return [pscustomobject]@{ExitCode=0;StdOut='';StdErr=''}};return [pscustomobject]@{ExitCode=0;StdOut='';StdErr=''} }
        Assert-Equal (Test-Throws { Move-WslDistribution -Name Debian -Destination (Join-Path $TestDrive 'moved') -WhatIf }) $true 'Move capability gate'
    }

    It 'parses malformed or empty WSL list output as no distributions' {
        Set-WslCommandExecutor { param($a,$f) [pscustomobject]@{ExitCode=0;StdOut='unexpected output';StdErr=''} }
        Assert-Equal @(Get-WslDistributions).Count 0 'Malformed list output'
    }

    It 'surfaces nonzero process exit codes' {
        Set-WslCommandExecutor { param($a,$f) [pscustomobject]@{ExitCode=17;StdOut='';StdErr='access denied'} }
        Assert-Equal (Test-Throws { Invoke-Wsl -ArgumentList @('--terminate','Debian') }) $true 'Nonzero WSL exit code'
    }

    It 'previews a backup with a path containing spaces without creating files' {
        $folder=Join-Path $TestDrive 'WSL Backup Folder';New-Item -ItemType Directory -Path $folder | Out-Null
        $planned=Backup-WslDistribution -Name Debian -Destination $folder -Format Tar -WhatIf
        Assert-Equal $planned.Planned $true 'WhatIf plan'
        Assert-Equal (Get-ChildItem -LiteralPath $folder).Count 0 'No preview side effects'
    }

    It 'refuses to unregister a known system-managed distribution' {
        Assert-Equal (Test-Throws { Invoke-DistributionAction -Action Unregister -Name 'docker-desktop' -WhatIf }) $true 'Protected unregister'
    }

    It 'writes structured operation logs to the configured application data directory' {
        Set-WslManagerDataDirectory $TestDrive
        Invoke-DistributionAction -Action Start -Name Debian | Out-Null
        $entry=Get-Content -LiteralPath (Join-Path $TestDrive 'operations.jsonl') | ConvertFrom-Json
        Assert-Equal $entry.Operation 'Start' 'Log operation'
        Assert-Equal $entry.Target 'Debian' 'Log target'
        Assert-Equal $entry.Result 'Succeeded' 'Log result'
        if(-not $entry.Timestamp -or $null -eq $entry.DurationMs){throw 'Log timestamp or duration is missing.'}
    }
}
