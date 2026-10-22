[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
param(
    [string]$Action,
    [string[]]$Distro,
    [string]$Destination,
    [string]$Source,
    [string]$Format='Tar',
    [string]$NewName,
    [string]$OutputFormat='Human',
    [string]$Version,
    [string]$Size,
    [switch]$Force,
    [switch]$Help
)

$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'src/Core/WSLManager.psm1') -Force

function Write-Result($Value) {
    if ($OutputFormat -eq 'Json') { $Value | ConvertTo-Json -Depth 12 }
    elseif ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) { $Value | Format-Table -AutoSize | Out-String | Write-Host }
    else { $Value | Format-List | Out-String | Write-Host }
}

function Show-Menu {
    while ($true) {
        try { Clear-Host } catch { Write-Verbose 'Terminal cannot clear its current screen.' }
        Write-Host 'WSL MANAGER v1.0' -ForegroundColor Cyan
        Write-Host '----------------'
        try {
            $distros=@(Get-WslDistributions)
            if($distros.Count){$distros | Format-Table Name,Version,State,IsDefault,IsProtected -AutoSize | Out-Host}else{Write-Host 'No registered distributions found.'}
        } catch { Write-Host ('WSL discovery failed: '+$_.Exception.Message) -ForegroundColor Yellow }
        Write-Host ''
        Write-Host '1. Distribution Manager'
        Write-Host '2. Backup & Restore'
        Write-Host '3. Move & Clone'
        Write-Host '4. Disk Management'
        Write-Host '5. WSL Configuration'
        Write-Host '6. Diagnostics & Reports'
        Write-Host '7. Settings & Logs'
        Write-Host '0. Exit'
        $pick=Read-Host 'Select'
        try {
            switch($pick){
                '1' { $n=Read-Host 'Distribution name'; $op=Read-Host 'Action (Info, Start, Terminate, SetDefault, Convert, Unregister)'; if($op -eq 'Info'){Write-Result (Get-WslDistroDetails $n)}elseif($op -eq 'Convert'){$v=[int](Read-Host 'Target WSL version (1 or 2)'); Invoke-DistributionAction -Action $op -Name $n -Version $v -Confirm}else{Invoke-DistributionAction -Action $op -Name $n -Confirm}; [void](Read-Host 'Press Enter to return') }
                '2' { $op=Read-Host 'Action (Backup or Restore)'; if($op -eq 'Backup'){$n=Read-Host 'Distribution';$path=Read-Host 'Existing destination directory';$fmt=Read-Host 'Format (Tar or Vhdx)';Backup-WslDistribution -Name $n -Destination $path -Format $fmt -Confirm|Write-Result}else{$src=Read-Host 'Backup path';$n=Read-Host 'New distribution name';$path=Read-Host 'New installation directory';Restore-WslBackup -Source $src -Name $n -Destination $path -Confirm|Write-Result}; [void](Read-Host 'Press Enter to return') }
                '3' { $op=Read-Host 'Action (Move or Clone)';$n=Read-Host 'Source distribution';$path=Read-Host 'Destination directory';if($op -eq 'Move'){Move-WslDistribution -Name $n -Destination $path -Confirm|Write-Result}else{$new=Read-Host 'New distribution name';Copy-WslDistribution -Name $n -NewName $new -Destination $path -Confirm|Write-Result}; [void](Read-Host 'Press Enter to return') }
                '4' { $op=Read-Host 'Action (Inspect or Resize)';if($op -eq 'Resize'){$n=Read-Host 'Distribution';$size=Read-Host 'New capacity (for example 128GB)';Resize-WslDistribution -Name $n -Size $size -Confirm|Write-Result}else{$diag=Get-ManagerDiagnostics;[pscustomobject]@{Volumes=$diag.Volumes;Distributions=@($diag.Distributions|ForEach-Object{Get-WslDistroDetails $_.Name})}|Write-Result}; [void](Read-Host 'Press Enter to return') }
                '5' { $p=Join-Path $HOME '.wslconfig';if(Test-Path $p){Get-Content -LiteralPath $p}else{Write-Host 'No .wslconfig found.'};Write-Host 'Configuration is read-only in v1.0.'; [void](Read-Host 'Press Enter to return') }
                '6' { Write-Result (Get-ManagerDiagnostics); [void](Read-Host 'Press Enter to return') }
                '7' { $p=Join-Path $env:LOCALAPPDATA 'WSLManager/operations.jsonl';if(Test-Path $p){Get-Content -LiteralPath $p | Select-Object -Last 30}else{Write-Host 'No operation logs yet.'}; [void](Read-Host 'Press Enter to return') }
                '0' { return }
                default { Write-Host 'Choose a listed number.' -ForegroundColor Yellow; Start-Sleep -Milliseconds 800 }
            }
        } catch { Write-Host ('Operation failed: '+$_.Exception.Message) -ForegroundColor Red; [void](Read-Host 'Press Enter to return') }
    }
}

try {
    if($Help -or $Action -eq 'Help'){Write-Host 'WSL Manager v1.0.0';Write-Host 'Usage: ./WSL-Manager.ps1 -Action <List|Info|Start|Terminate|SetDefault|Convert|Unregister|Backup|Restore|Move|Clone|Diagnose|Disk|Resize>';Write-Host 'Run without -Action to open the interactive menu. Use -WhatIf to preview mutations.';return}
    if(-not $Action){ if([Console]::IsInputRedirected){throw 'Interactive menu requires a terminal. Use -Help or specify -Action.'}; Show-Menu; return }
    if($Action -notin @('List','Info','Start','Terminate','SetDefault','Convert','Unregister','Backup','Restore','Move','Clone','Diagnose','Disk','Resize')){throw "Unknown action '$Action'. Use -Help for supported actions."}
    if($Format -notin @('Tar','Vhdx')){throw "Unsupported format '$Format'. Choose Tar or Vhdx."}
    if($OutputFormat -notin @('Human','Json')){throw "Unsupported output format '$OutputFormat'. Choose Human or Json."}
    if($PSBoundParameters.ContainsKey('Version') -and $Version -notin @('1','2')){throw 'Version must be 1 or 2.'}
    if($Distro -and $Distro.Count -gt 1 -and $Action -ne 'Backup'){throw 'Only Backup accepts multiple -Distro values.'}
    $result=$null
    switch($Action){
        List {$result=@(Get-WslDistributions)}
        Info {if(-not $Distro){throw '-Distro is required for Info.'};$result=Get-WslDistroDetails $Distro[0]}
        Diagnose {$result=Get-ManagerDiagnostics}
        Disk {if($Distro){$result=Get-WslDistroDetails $Distro[0]}else{$diag=Get-ManagerDiagnostics;$result=[pscustomobject]@{Volumes=$diag.Volumes;Distributions=$diag.Distributions;Explanation='Linux filesystem usage, VHDX allocated file size, and VHDX virtual capacity are different measurements.'}}}
        Start {if(-not $Distro){throw '-Distro is required.'};$result=Invoke-DistributionAction -Action Start -Name $Distro[0] -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Terminate {if(-not $Distro){throw '-Distro is required.'};$result=Invoke-DistributionAction -Action Terminate -Name $Distro[0] -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        SetDefault {if(-not $Distro){throw '-Distro is required.'};$result=Invoke-DistributionAction -Action SetDefault -Name $Distro[0] -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Convert {if(-not $Distro -or -not $PSBoundParameters.ContainsKey('Version')){throw '-Distro and -Version (1 or 2) are required.'};$result=Invoke-DistributionAction -Action Convert -Name $Distro[0] -Version $Version -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Unregister {if(-not $Distro){throw '-Distro is required.'};$result=Invoke-DistributionAction -Action Unregister -Name $Distro[0] -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Backup {if(-not $Distro -or -not $Destination){throw '-Distro (one or more names) and -Destination are required.'};$result=@(foreach($name in $Distro){Backup-WslDistribution -Name $name -Destination $Destination -Format $Format -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))})}
        Restore {if(-not $Source -or -not $Distro -or -not $Destination){throw '-Source, -Distro (new name), and -Destination are required.'};$result=Restore-WslBackup -Source $Source -Name $Distro[0] -Destination $Destination -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Move {if(-not $Distro -or -not $Destination){throw '-Distro and -Destination are required.'};$result=Move-WslDistribution -Name $Distro[0] -Destination $Destination -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Clone {if(-not $Distro -or -not $NewName -or -not $Destination){throw '-Distro, -NewName, and -Destination are required.'};$result=Copy-WslDistribution -Name $Distro[0] -NewName $NewName -Destination $Destination -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
        Resize {if(-not $Distro -or -not $Size){throw '-Distro and -Size (for example 128GB) are required.'};$result=Resize-WslDistribution -Name $Distro[0] -Size $Size -WhatIf:$WhatIfPreference -Confirm:($PSBoundParameters.ContainsKey('Confirm'))}
    }
    Write-Result $result
} catch {
    [Console]::Error.WriteLine(('WSL Manager error: '+$_.Exception.Message))
    throw
}
