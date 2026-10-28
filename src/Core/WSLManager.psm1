Set-StrictMode -Version 2

$wslCommand = Get-Command wsl.exe -ErrorAction SilentlyContinue
$script:WslExe = if ($null -ne $wslCommand) { $wslCommand.Source } else { $null }
$script:AppRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:DataRoot = Join-Path $env:LOCALAPPDATA 'WSLManager'
$script:WslExecutor = $null

function Set-WslCommandExecutor {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='Low')]
    param([scriptblock]$Executor)
    if($PSCmdlet.ShouldProcess('WSL command adapter','Set test command executor')){$script:WslExecutor = $Executor}
}

function Set-WslManagerDataDirectory {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='Low')]
    param([Parameter(Mandatory)][string]$Path)
    $resolved=[IO.Path]::GetFullPath($Path)
    if($PSCmdlet.ShouldProcess($resolved,'Set application data directory')){$script:DataRoot = $resolved}
}

function ConvertFrom-WslText {
    param([AllowNull()][byte[]]$Bytes)
    if ($null -eq $Bytes -or $Bytes.Length -eq 0) { return '' }
    if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) { $encoding=[Text.Encoding]::Unicode; $offset=2 }
    elseif ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) { $encoding=[Text.Encoding]::BigEndianUnicode; $offset=2 }
    else {
        $zeroEven=0; $zeroOdd=0
        for($i=0;$i -lt $Bytes.Length;$i++){if($Bytes[$i] -eq 0){if(($i % 2) -eq 0){$zeroEven++}else{$zeroOdd++}}}
        if(($zeroEven+$zeroOdd) -gt 0 -and $zeroOdd -gt ($Bytes.Length/8)){$encoding=[Text.Encoding]::Unicode}else{$encoding=[Text.Encoding]::UTF8}
        $offset=0
    }
    return $encoding.GetString($Bytes,$offset,$Bytes.Length-$offset).Trim([char]0,[char]13,[char]10,[char]32,[char]0xFEFF)
}

function ConvertTo-WindowsArgument {
    param([AllowEmptyString()][string]$Value)
    if ($Value -notmatch '[\s"]') { return $Value }
    $builder = New-Object Text.StringBuilder
    [void]$builder.Append('"'); $slashes = 0
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '\') { $slashes++; continue }
        if ($ch -eq '"') { [void]$builder.Append(('\' * (($slashes * 2) + 1))); [void]$builder.Append('"'); $slashes = 0; continue }
        if ($slashes) { [void]$builder.Append(('\' * $slashes)); $slashes = 0 }
        [void]$builder.Append($ch)
    }
    if ($slashes) { [void]$builder.Append(('\' * ($slashes * 2))) }
    [void]$builder.Append('"'); return $builder.ToString()
}

function Invoke-Wsl {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$ArgumentList, [switch]$AllowFailure)
    if ($script:WslExecutor) {
        $mockResult=& $script:WslExecutor $ArgumentList $AllowFailure
        if($mockResult.ExitCode -ne 0 -and -not $AllowFailure){throw ("Mock WSL command failed ({0}): {1}" -f $mockResult.ExitCode,$mockResult.StdErr)}
        return $mockResult
    }
    if (-not $script:WslExe) { throw 'wsl.exe was not found on PATH.' }
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $script:WslExe; $psi.Arguments = (($ArgumentList | ForEach-Object { ConvertTo-WindowsArgument ([string]$_) }) -join ' ')
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process; $process.StartInfo = $psi
    if (-not $process.Start()) { throw 'Could not start wsl.exe.' }
    $outBytes=New-Object IO.MemoryStream; $errBytes=New-Object IO.MemoryStream
    $outTask=$process.StandardOutput.BaseStream.CopyToAsync($outBytes); $errTask=$process.StandardError.BaseStream.CopyToAsync($errBytes)
    $process.WaitForExit(); $null=$outTask.GetAwaiter().GetResult(); $null=$errTask.GetAwaiter().GetResult()
    $stdout = ConvertFrom-WslText $outBytes.ToArray(); $stderr = ConvertFrom-WslText $errBytes.ToArray()
    $result = [pscustomobject]@{ ExitCode=$process.ExitCode; StdOut=$stdout; StdErr=$stderr; Arguments=$ArgumentList }
    if ($process.ExitCode -ne 0 -and -not $AllowFailure) { throw ("WSL command failed ({0}): {1}" -f $process.ExitCode, $(if ($stderr) {$stderr} else {$stdout})) }
    return $result
}

function Get-WslCapabilities {
    $available = [bool]$script:WslExe; $version = $null; $help = ''; $status = ''
    if ($available) {
        $v = Invoke-Wsl -ArgumentList @('--version') -AllowFailure; $version = $v.StdOut; if (-not $version) { $version=$v.StdErr }
        $h = Invoke-Wsl -ArgumentList @('--help') -AllowFailure; $help = $h.StdOut + "`n" + $h.StdErr
        $s = Invoke-Wsl -ArgumentList @('--status') -AllowFailure; $status=$s.StdOut + "`n" + $s.StdErr
    }
    [pscustomobject]@{ Available=$available; Version=$version; Status=$status.Trim(); SupportsVhdExport=($help -match '--vhd'); SupportsImportInPlace=($help -match '--import-in-place'); SupportsSetVersion=($help -match '--set-version'); SupportsManage=($help -match '--manage'); SupportsManageMove=($help -match '--move'); SupportsResize=($help -match '--resize'); Help=$help.Trim() }
}

function Get-WslDistributions {
    $r = Invoke-Wsl -ArgumentList @('--list','--verbose')
    $lines = $r.StdOut -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    $default = $null; $items = @()
    foreach ($line in $lines) {
        if ($line -match 'Default Distribution:\s*(.+)$') { $default=$Matches[1].Trim(); continue }
        if ($line -match '^(\*)?\s*(.+?)\s{2,}(.+?)\s+(\d+)\s*$') {
            $name=$Matches[2].Trim(); $items += [pscustomobject]@{Name=$name; State=$Matches[3]; Version=[int]$Matches[4]; IsDefault=([bool]$Matches[1] -or $name -eq $default); IsProtected=($name -match '^(docker-desktop(?:-data)?|podman-machine-default)$')}
        }
    }
    return $items
}

function Get-WslRunningNames {
    $r=Invoke-Wsl -ArgumentList @('--list','--running','--quiet')
    $known=@(Get-WslDistributions | ForEach-Object {$_.Name}); $running=@()
    foreach($line in ($r.StdOut -split "`r?`n")){
        $name=$line.Trim()
        if(-not $name){continue}
        if($known -ccontains $name){$running+=$name}else{throw 'Could not reliably interpret `wsl --list --running --quiet`; operation refused.'}
    }
    return $running
}

function Get-WslDistro {
    param([Parameter(Mandatory)][string]$Name)
    $d = @(Get-WslDistributions | Where-Object { $_.Name -ceq $Name })
    if ($d.Count -ne 1) { throw "Distribution '$Name' was not found. Names must match exactly." }
    return $d[0]
}

function Assert-WslDistroOperationAllowed {
    param([Parameter(Mandatory)]$Distribution,[Parameter(Mandatory)][string]$Operation)
    if($Distribution.IsProtected -and $Operation -in @('Terminate','SetDefault','Convert','Unregister','Move','Clone','Resize')){
        throw "Operation '$Operation' is refused for protected system-managed distribution '$($Distribution.Name)'."
    }
}

function Get-WslDistroDetails {
    param([Parameter(Mandatory)][string]$Name)
    $d=Get-WslDistro $Name; $location=$null; $vhd=$null; $vhdReason='A registered VHDX location could not be reliably discovered.'; $hostVolume=$null; $hostVolumeReason='Host volume free space is unavailable because its local drive could not be identified.'; $linuxStorage=$null; $wslConf=$null
    $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
    if(Test-Path -LiteralPath $key){
        foreach($entry in (Get-ChildItem -LiteralPath $key -ErrorAction SilentlyContinue)){
            $meta=Get-ItemProperty -LiteralPath $entry.PSPath -ErrorAction SilentlyContinue
            if($meta.DistributionName -ceq $Name -and $meta.BasePath -and (Test-Path -LiteralPath $meta.BasePath -PathType Container)){
                $location=[IO.Path]::GetFullPath([string]$meta.BasePath)
                # Join-Path's FileSystem provider rejects device paths (\\?\C:\...) in Windows PowerShell 5.1.
                $candidate=[IO.Path]::Combine($location,'ext4.vhdx')
                if(Test-Path -LiteralPath $candidate -PathType Leaf){
                    $file=Get-Item -LiteralPath $candidate
                    $capacity=$null; $capacityReason=''
                    if($script:WslExecutor){$capacityReason='Virtual capacity inspection skipped while the WSL adapter is mocked.'}
                    elseif(-not (Get-Command Get-VHD -ErrorAction SilentlyContinue)){$capacityReason='Virtual capacity is unavailable because the read-only Get-VHD cmdlet is not installed.'}
                    else{
                        try{$capacity=[long](Get-VHD -Path $candidate -ErrorAction Stop).Size}
                        catch{$capacityReason='Virtual capacity is unavailable because Get-VHD could not inspect this disk with current permissions or while it is in use.'}
                    }
                    $vhd=[pscustomobject]@{Path=$candidate;SizeBytes=[long]$file.Length;VirtualCapacityBytes=$capacity;CapacityUnavailableReason=if($null -eq $capacity){$capacityReason}else{$null};LastWriteTimeUtc=$file.LastWriteTimeUtc.ToString('o')}
                    $vhdReason=$null
                    $driveMatch=[regex]::Match($candidate,'^(?:\\\\\?\\)?(?<drive>[A-Za-z]):\\')
                    if($driveMatch.Success){
                        $hostDrive=Get-PSDrive -Name $driveMatch.Groups['drive'].Value -PSProvider FileSystem -ErrorAction SilentlyContinue
                        if($hostDrive){$hostVolume=[pscustomobject]@{Name=$hostDrive.Name;Root=$hostDrive.Root;FreeBytes=[long]$hostDrive.Free;UsedBytes=[long]$hostDrive.Used};$hostVolumeReason=$null}
                    }
                }else{$vhdReason='The ext4.vhdx file was not present at the reliably discovered registration path.'}
                break
            }
        }
    }
    if($d.State -match '^(?i:Running)$' -and -not $d.IsProtected){
        $conf=Invoke-Wsl -ArgumentList @('--distribution',$Name,'--exec','cat','/etc/wsl.conf') -AllowFailure
        if($conf.ExitCode -eq 0){
            $settings=@{}; $section=''
            foreach($line in ($conf.StdOut -split "`r?`n")){
                $clean=$line.Trim(); if(-not $clean -or $clean.StartsWith('#') -or $clean.StartsWith(';')){continue}
                if($clean -match '^\[([^\]]+)\]$'){$section=$Matches[1].Trim().ToLowerInvariant();continue}
                if($clean -match '^([^=]+)=(.*)$'){$key=$Matches[1].Trim().ToLowerInvariant();$value=$Matches[2].Trim();if($section -eq 'boot' -and $key -eq 'command'){$value='[redacted]'};$settings[($section+'.'+$key)]=$value}
            }
            $wslConf=[pscustomobject]@{Accessible=$true;Settings=$settings;BootCommandRedacted=$true}
        }else{$wslConf=[pscustomobject]@{Accessible=$false;Reason='File is absent or inaccessible; distro was not started.'}}
        $df=Invoke-Wsl -ArgumentList @('--distribution',$Name,'--exec','df','-B1','--output=size,used,avail,pcent','/') -AllowFailure
        if($df.ExitCode -eq 0){
            $row=($df.StdOut -split "`r?`n" | Where-Object {$_ -match '^\s*\d+\s+\d+\s+\d+'} | Select-Object -Last 1)
            if($row -match '^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+%)'){$linuxStorage=[pscustomobject]@{FilesystemBytes=[long]$Matches[1];UsedBytes=[long]$Matches[2];AvailableBytes=[long]$Matches[3];UsePercent=$Matches[4]}}
        }
    }
    [pscustomobject]@{Name=$d.Name;Version=$d.Version;State=$d.State;IsDefault=$d.IsDefault;IsProtected=$d.IsProtected;ProtectionBasis=if($d.IsProtected){'Known system-managed distribution name'}else{'No known protected-name match; WSL does not expose a protected flag'};InstallationPath=$location;VirtualDisk=$vhd;VirtualDiskUnavailableReason=$vhdReason;HostVolume=$hostVolume;HostVolumeUnavailableReason=$hostVolumeReason;LinuxFilesystem=$linuxStorage;WslConf=$wslConf}
}

function Write-OperationLog {
    param([string]$Operation,[string]$Target,[string]$Result,[double]$DurationMs,[string]$ErrorMessage)
    if (-not (Test-Path -LiteralPath $script:DataRoot)) { New-Item -ItemType Directory -Path $script:DataRoot -Force | Out-Null }
    $safeError=[string]$ErrorMessage
    $safeError=$safeError -replace '[\r\n\t]+',' '
    $safeError=$safeError -replace '(?i)(password|token|secret|credential)\s*[:=]\s*\S+','$1=<redacted>'
    $safeError=$safeError -replace '(?i)[A-Z]:\\[^\s"''<>|]+','<path>'
    if($safeError.Length -gt 500){$safeError=$safeError.Substring(0,500)}
    $row=[pscustomobject]@{Timestamp=(Get-Date).ToUniversalTime().ToString('o');Operation=$Operation;Target=$Target;DurationMs=[math]::Round($DurationMs,0);Result=$Result;Error=$safeError}
    $path=Join-Path $script:DataRoot 'operations.jsonl'; Add-Content -LiteralPath $path -Value ($row | ConvertTo-Json -Compress) -Encoding UTF8
    $days=90; $configured=0
    if([int]::TryParse($env:WSL_MANAGER_LOG_RETENTION_DAYS,[ref]$configured) -and $configured -ge 1 -and $configured -le 3650){$days=$configured}
    if(Test-Path -LiteralPath $path){
        $kept=@(); foreach($line in (Get-Content -LiteralPath $path -ErrorAction SilentlyContinue)){try{$entry=$line|ConvertFrom-Json;if([datetime]::Parse($entry.Timestamp).ToUniversalTime() -ge (Get-Date).ToUniversalTime().AddDays(-$days)){$kept+=$line}}catch{$kept+=$line}}
        Set-Content -LiteralPath $path -Value $kept -Encoding UTF8
    }
}

function Invoke-LoggedOperation {
    param([string]$Operation,[string]$Target,[scriptblock]$Body)
    $watch=[Diagnostics.Stopwatch]::StartNew(); $result='Failed'; $err=$null
    try { $value=& $Body; $result='Succeeded'; return $value }
    catch { $err=$_.Exception.Message; throw }
    finally { $watch.Stop(); Write-OperationLog $Operation $Target $result $watch.Elapsed.TotalMilliseconds $err }
}

function Get-ManagerDiagnostics {
    $cap=Get-WslCapabilities; $d=@(); $discoveryError=$null
    if($cap.Available){try{$d=@(Get-WslDistributions | ForEach-Object { Get-WslDistroDetails $_.Name })}catch{$discoveryError=$_.Exception.Message}}
    $drives=Get-PSDrive -PSProvider FileSystem | ForEach-Object { [pscustomobject]@{Name=$_.Name;Root=$_.Root;FreeBytes=$_.Free;UsedBytes=$_.Used} }
    $globalConfig=Join-Path $HOME '.wslconfig'
    $wslConfig=$null
    if(Test-Path -LiteralPath $globalConfig -PathType Leaf){$wslConfig=[pscustomobject]@{Path=$globalConfig;Content=[IO.File]::ReadAllText($globalConfig)}}
    [pscustomobject]@{ApplicationVersion='1.0.0';Computer=$env:COMPUTERNAME;Windows=[Environment]::OSVersion.Version.ToString();PowerShell=$PSVersionTable.PSVersion.ToString();Capabilities=$cap;Distributions=$d;DistributionDiscoveryError=$discoveryError;Volumes=@($drives);WslConfig=$wslConfig;GeneratedAt=(Get-Date).ToUniversalTime().ToString('o')}
}

function Get-BackupFileName {
    param([string]$Name,[string]$Format)
    $safe=$Name -replace '[^A-Za-z0-9._-]','_'; $ext=if($Format -eq 'Vhdx'){'vhdx'}else{'tar'}
    '{0}_{1}_{2}.{3}' -f $safe,(Get-Date -Format 'yyyyMMdd_HHmmss'),([guid]::NewGuid().ToString('N').Substring(0,8)),$ext
}

function Backup-WslDistribution {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='Medium')]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Destination,[ValidateSet('Tar','Vhdx')][string]$Format='Tar')
    $d=Get-WslDistro $Name; if($Format -eq 'Vhdx' -and $d.Version -ne 2){throw 'VHDX export is supported only for WSL 2 distributions.'}
    if($Format -eq 'Vhdx' -and -not (Get-WslCapabilities).SupportsVhdExport){throw 'Installed WSL does not advertise VHDX export support.'}
    $dir=[IO.Path]::GetFullPath($Destination); if(-not(Test-Path -LiteralPath $dir -PathType Container)){throw "Destination directory does not exist: $dir"}
    $file=Join-Path $dir (Get-BackupFileName $Name $Format); $staging=$file+'.partial'
    if((Test-Path -LiteralPath $file) -or (Test-Path -LiteralPath ($file+'.json'))){throw 'Generated backup path already exists; refusing to overwrite.'}
    $drive=Get-PSDrive -Name ([IO.Path]::GetPathRoot($dir).TrimEnd('\').TrimEnd(':')) -ErrorAction SilentlyContinue
    if(-not $drive){throw 'Could not determine free space on the backup volume.'}
    $details=Get-WslDistroDetails $Name; $required=1GB
    if($details.VirtualDisk -and [long]$details.VirtualDisk.SizeBytes -gt $required){$required=[long]$details.VirtualDisk.SizeBytes}
    if($details.LinuxFilesystem -and [long]$details.LinuxFilesystem.UsedBytes -gt $required){$required=[long]$details.LinuxFilesystem.UsedBytes}
    if([long]$drive.Free -lt $required){throw ("Backup volume has {0:N0} free bytes; estimated minimum is {1:N0}." -f $drive.Free,$required)}
    if(-not $PSCmdlet.ShouldProcess($Name,"Export $Format backup to $file")){return [pscustomobject]@{Planned=$true;Path=$file}}
    Invoke-LoggedOperation 'Backup' $Name {
        if(Test-Path -LiteralPath $staging){throw 'Staging path already exists.'}
        $wslArguments=@('--export',$Name,$staging); if($Format -eq 'Vhdx'){$wslArguments+=@('--vhd')}
        # WSL syntax is --export <name> <file> [--vhd].
        if($Format -eq 'Vhdx'){$wslArguments=@('--export',$Name,$staging,'--vhd')}
        Invoke-Wsl -ArgumentList $wslArguments | Out-Null
        if(-not(Test-Path -LiteralPath $staging -PathType Leaf)){throw 'WSL reported success but no backup file was created.'}
        $info=Get-Item -LiteralPath $staging; if($info.Length -lt 1MB){throw 'Backup output is unexpectedly small; partial file retained.'}
        $hash=Get-FileHash -LiteralPath $staging -Algorithm SHA256
        $final=$file; Move-Item -LiteralPath $staging -Destination $final
        $manifest=[pscustomobject]@{SchemaVersion=1;Distribution=$Name;WslVersion=$d.Version;Format=$Format;CreatedAt=(Get-Date).ToUniversalTime().ToString('o');File=(Split-Path -Leaf $final);SizeBytes=$info.Length;Sha256=$hash.Hash;ProcessSucceeded=$true;FormatChecked=$true;TestRestoreSucceeded=$false;Verification='Export completed, file-size checked, SHA-256 recorded. Restore not tested.'}
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ($final+'.json') -Encoding UTF8
        $hist=Join-Path $script:DataRoot 'backup-history.jsonl'; Add-Content -LiteralPath $hist -Value ($manifest | ConvertTo-Json -Compress) -Encoding UTF8
        return $manifest
    }
}

function Restore-WslBackup {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Destination)
    $src=[IO.Path]::GetFullPath($Source); if(-not(Test-Path -LiteralPath $src -PathType Leaf)){throw "Backup file not found: $src"}
    $dest=[IO.Path]::GetFullPath($Destination); if(Test-Path -LiteralPath $dest){throw "Destination already exists; restore never overwrites: $dest"}
    if(@(Get-WslDistributions | Where-Object {$_.Name -ceq $Name}).Count){throw "Distribution already registered: $Name"}
    $ext=[IO.Path]::GetExtension($src).ToLowerInvariant(); if($ext -notin @('.tar','.vhdx')){throw 'Only .tar and .vhdx backups are supported.'}
    $root=[IO.Path]::GetPathRoot($dest); $letter=$root.Substring(0,1); $volume=Get-PSDrive -Name $letter -ErrorAction SilentlyContinue
    if(-not $volume){throw 'Could not determine free space on the restore volume.'}
    $sourceBytes=(Get-Item -LiteralPath $src).Length
    if([long]$volume.Free -lt $sourceBytes){throw 'Restore volume has less free space than the backup file; restore refused.'}
    if(-not $PSCmdlet.ShouldProcess($Name,"Restore $src to $dest")){return [pscustomobject]@{Planned=$true;Name=$Name;Destination=$dest}}
    Invoke-LoggedOperation 'Restore' $Name {
        New-Item -ItemType Directory -Path $dest | Out-Null
        try {
            if($ext -eq '.vhdx') { $cap=Get-WslCapabilities; if(-not $cap.SupportsVhdExport){throw 'Installed WSL does not advertise VHDX import/export support.'}; Invoke-Wsl -ArgumentList @('--import',$Name,$dest,$src,'--vhd') | Out-Null }
            else { Invoke-Wsl -ArgumentList @('--import',$Name,$dest,$src,'--version','2') | Out-Null }
            $d=Get-WslDistro $Name
            Invoke-Wsl -ArgumentList @('--distribution',$Name,'--exec','/bin/true') | Out-Null
            return [pscustomobject]@{Name=$Name;Destination=$dest;Version=$d.Version;BootTestSucceeded=$true;DefaultUser='root (WSL imports default to root unless the distro config specifies otherwise)'}
        } catch { throw ("Restore failed; destination data preserved at {0}. Manual review may be required. {1}" -f $dest,$_.Exception.Message) }
    }
}

function Invoke-DistributionAction {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='Medium')]
    param([ValidateSet('Start','Terminate','SetDefault','Convert','Unregister')][string]$Action,[Parameter(Mandatory)][string]$Name,[int]$Version)
    $d=Get-WslDistro $Name
    Assert-WslDistroOperationAllowed -Distribution $d -Operation $Action
    if($Action -eq 'Convert' -and $Version -notin 1,2){throw 'Conversion requires -Version 1 or -Version 2.'}
    if($Action -eq 'Convert' -and $d.Version -eq $Version){throw "Distribution is already WSL $Version."}
    if($Action -eq 'Convert' -and -not (Get-WslCapabilities).SupportsSetVersion){throw 'Installed WSL does not advertise `wsl --set-version`; conversion is unsupported.'}
    $description=switch($Action){Start{'Start distribution'} Terminate{'Terminate selected distribution'} SetDefault{'Set default distribution'} Convert{"Convert to WSL $Version"} Unregister{'PERMANENTLY unregister and delete all distribution data'}}
    if(-not $PSCmdlet.ShouldProcess($Name,$description)){return}
    if($Action -eq 'Unregister') {
        # Exact-name confirmation remains mandatory; -Force must never bypass this destructive-action gate.
        if(-not $PSCmdlet.ShouldContinue("This permanently deletes all data for '$Name'. A verified backup is strongly recommended. Continue to the name check?",'Destructive operation')){return}
        $typed=Read-Host "Type $Name exactly to permanently unregister it"
        if($typed -cne $Name){throw 'Typed distribution name did not match; unregister cancelled.'}
    }
    Invoke-LoggedOperation $Action $Name {
        switch($Action){
            Start {Invoke-Wsl -ArgumentList @('--distribution',$Name,'--exec','/bin/true') | Out-Null}
            Terminate {Invoke-Wsl -ArgumentList @('--terminate',$Name) | Out-Null}
            SetDefault {Invoke-Wsl -ArgumentList @('--set-default',$Name) | Out-Null}
            Convert {Invoke-Wsl -ArgumentList @('--set-version',$Name,[string]$Version) | Out-Null}
            Unregister {if($d.IsProtected){throw 'Protected distribution.'}; Invoke-Wsl -ArgumentList @('--unregister',$Name) | Out-Null}
        }
    }
}

function Move-WslDistribution {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Destination)
    $d=Get-WslDistro $Name; Assert-WslDistroOperationAllowed -Distribution $d -Operation 'Move'; if($d.Version -ne 2){throw 'Native move is supported only for WSL 2 distributions.'}
    $cap=Get-WslCapabilities; if(-not $cap.SupportsManageMove){throw 'Installed WSL does not advertise native `wsl --manage <distro> --move`; no fallback is attempted.'}
    $dest=[IO.Path]::GetFullPath($Destination); $destExists=Test-Path -LiteralPath $dest
    if($destExists -and (Get-ChildItem -LiteralPath $dest -Force | Measure-Object).Count){throw 'Destination exists and is not empty.'}
    $details=Get-WslDistroDetails $Name
    if(-not $details.VirtualDisk){throw 'VHDX path could not be discovered for storage preflight.'}
    $root=[IO.Path]::GetPathRoot($dest); $letter=$root.Substring(0,1); $volume=Get-Volume -DriveLetter $letter -ErrorAction SilentlyContinue
    if(-not $volume -or $volume.FileSystem -ne 'NTFS'){throw 'Move requires a verified local NTFS destination volume; volume type could not be confirmed.'}
    if([long]$volume.SizeRemaining -lt [long]$details.VirtualDisk.SizeBytes){throw 'Destination has less free space than the current VHDX allocated size; move refused.'}
    if(-not $PSCmdlet.ShouldProcess($Name,"Move WSL 2 data to $dest")){return [pscustomobject]@{Planned=$true;Destination=$dest}}
    Invoke-LoggedOperation 'Move' $Name { if(-not $destExists){New-Item -ItemType Directory -Path $dest | Out-Null}; Invoke-Wsl -ArgumentList @('--manage',$Name,'--move',$dest) | Out-Null; $after=Get-WslDistro $Name; if($after.Version -ne 2){throw 'Post-move registration verification failed.'}; [pscustomobject]@{Name=$Name;Destination=$dest;Verified=$true} }
}

function Copy-WslDistribution {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$NewName,[Parameter(Mandatory)][string]$Destination)
    $source=Get-WslDistro $Name; Assert-WslDistroOperationAllowed -Distribution $source -Operation 'Clone'
    if(@(Get-WslDistributions|Where-Object {$_.Name -ceq $NewName}).Count){throw "Distribution already registered: $NewName"}
    $dest=[IO.Path]::GetFullPath($Destination); if(Test-Path -LiteralPath $dest){throw "Destination already exists: $dest"}
    if(-not $PSCmdlet.ShouldProcess($NewName,"Clone $Name to $dest")){return [pscustomobject]@{Planned=$true;Name=$NewName;Destination=$dest}}
    Invoke-LoggedOperation 'Clone' $Name {
        $temp=Join-Path ([IO.Path]::GetTempPath()) ('wslmanager_'+[guid]::NewGuid().ToString('N')+'.tar')
        $cloneSucceeded=$false
        try { Invoke-Wsl -ArgumentList @('--export',$Name,$temp) | Out-Null; New-Item -ItemType Directory -Path $dest | Out-Null; Invoke-Wsl -ArgumentList @('--import',$NewName,$dest,$temp,'--version',[string]$source.Version) | Out-Null; Invoke-Wsl -ArgumentList @('--distribution',$NewName,'--exec','/bin/true') | Out-Null; $cloneSucceeded=$true; [pscustomobject]@{Name=$NewName;Destination=$dest;BootTestSucceeded=$true;SourcePreserved=$true} }
        catch { throw ("Clone failed; recoverable source export retained at {0}. {1}" -f $temp,$_.Exception.Message) }
        finally { if($cloneSucceeded -and (Test-Path -LiteralPath $temp)){Remove-Item -LiteralPath $temp -Force} }
    }
}

function ConvertTo-SizeBytes {
    param([Parameter(Mandatory)][string]$Size)
    if($Size -notmatch '^(\d+)(B|M|MB|G|GB|T|TB)?$'){throw 'Size must be an integer with optional B, M, MB, G, GB, T, or TB suffix.'}
    $n=[decimal]$Matches[1]; $unit=[string]$Matches[2]
    $power=switch -Regex ($unit) { '^(B)?$' {0} '^(M|MB)$' {2} '^(G|GB)$' {3} '^(T|TB)$' {4} }
    return [long]($n * [decimal][math]::Pow(1024,$power))
}

function Resize-WslDistribution {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Size)
    $d=Get-WslDistro $Name; Assert-WslDistroOperationAllowed -Distribution $d -Operation 'Resize'; if($d.Version -ne 2){throw 'Disk expansion is supported only for WSL 2 distributions.'}
    $cap=Get-WslCapabilities; if(-not $cap.SupportsResize){throw 'Installed WSL does not advertise `wsl --manage --resize`.'}
    $details=Get-WslDistroDetails $Name; if(-not $details.VirtualDisk){throw 'A registered ext4.vhdx path could not be discovered; expansion is refused.'}
    if(-not (Get-Command Get-VHD -ErrorAction SilentlyContinue)){throw 'Safe preflight needs the read-only Get-VHD cmdlet to inspect the current virtual capacity. Install the Hyper-V PowerShell module; WSL Manager will not parse or edit VHDX files directly.'}
    $vhd=Get-VHD -Path $details.VirtualDisk.Path -ErrorAction Stop; $requested=ConvertTo-SizeBytes $Size
    if($requested -le [long]$vhd.Size){throw 'Requested capacity must be larger than the current virtual capacity.'}
    $running=@(Get-WslRunningNames)
    if($running.Count){throw ('Microsoft documents wsl --shutdown before resize. These distributions are running: {0}. Stop them yourself; WSL Manager will not shut down the global WSL VM.' -f ($running -join ', '))}
    $root=[IO.Path]::GetPathRoot($details.VirtualDisk.Path); $drive=Get-PSDrive -PSProvider FileSystem | Where-Object {$_.Root -eq $root} | Select-Object -First 1
    if(-not $drive){throw 'Could not determine free space on the distribution volume.'}
    $required=$requested-[long]$vhd.Size
    if([long]$drive.Free -lt $required){throw 'Available volume space is below the requested virtual-disk growth; resize refused.'}
    if(-not $PSCmdlet.ShouldProcess($Name,"Expand virtual disk from $($vhd.Size) bytes to $requested bytes")){return [pscustomobject]@{Planned=$true;Name=$Name;CurrentBytes=[long]$vhd.Size;RequestedBytes=$requested;RequiredFreeBytes=$required}}
    Invoke-LoggedOperation 'Resize' $Name {Invoke-Wsl -ArgumentList @('--manage',$Name,'--resize',$Size) | Out-Null;$verified=Get-VHD -Path $details.VirtualDisk.Path -ErrorAction Stop;if([long]$verified.Size -lt $requested){throw 'WSL returned success but post-operation virtual capacity is below target.'};[pscustomobject]@{Name=$Name;CurrentBytes=[long]$vhd.Size;CapacityBytes=[long]$verified.Size;Verified=$true}}
}

Export-ModuleMember -Function Invoke-Wsl,Set-WslCommandExecutor,Set-WslManagerDataDirectory,Get-WslCapabilities,Get-WslDistributions,Get-WslRunningNames,Get-WslDistro,Get-WslDistroDetails,Get-ManagerDiagnostics,Backup-WslDistribution,Restore-WslBackup,Invoke-DistributionAction,Move-WslDistribution,Copy-WslDistribution,Resize-WslDistribution
