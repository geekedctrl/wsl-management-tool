[CmdletBinding()]
param([string] $InnoCompilerPath, [string] $OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\WSLManager.Desktop\WSLManager.Desktop.csproj'
$iss = Join-Path $root 'packaging\installer\WSLManager.iss'
$out = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root 'artifacts\installer' }
$installer = Join-Path $out 'WSL-Manager-Setup-1.0.0-x64.exe'
if (Test-Path -LiteralPath $installer) { throw "Refusing to overwrite existing installer: $installer" }
if (-not $InnoCompilerPath) { $InnoCompilerPath = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe' }
if (-not (Test-Path -LiteralPath $InnoCompilerPath -PathType Leaf)) { throw 'Install Inno Setup 7 to build the installer.' }
$dotnet = Join-Path $env:LOCALAPPDATA 'WSLManager\dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source }
$sdk = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [version]$sdk -lt [version]'10.0.100') { throw "Requires .NET 10 SDK; found $sdk" }
$stage = Join-Path ([IO.Path]::GetTempPath()) "WSLManager-Publish-$([guid]::NewGuid().ToString('N'))"
$built = $false
try {
  New-Item -ItemType Directory -Path $stage,$out -Force | Out-Null
  $args = @('publish',$project,'--configuration','Release','--runtime','win-x64','--self-contained','true','--output',$stage,'-p:Platform=x64','-p:WindowsAppSDKSelfContained=true','-p:EnableMsixTooling=true','-p:PublishTrimmed=false','-p:PublishReadyToRun=false','-p:DebugType=None')
  & $dotnet @args
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
  foreach ($rel in @('WSLManager.Desktop.exe','backend\WSL-Manager.ps1','backend\src\Core\WSLManager.psm1')) { if (-not (Test-Path -LiteralPath (Join-Path $stage $rel) -PathType Leaf)) { throw "Missing published file: $rel" } }
  $env:WSL_MANAGER_PUBLISH_DIR = $stage; $env:WSL_MANAGER_INSTALLER_OUTPUT_DIR = $out; $env:WSL_MANAGER_REPOSITORY_ROOT = $root
  & $InnoCompilerPath $iss
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Inno Setup compilation failed.' }
  $built = $true; $item = Get-Item -LiteralPath $installer; $hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
  Write-Host "Installer: $($item.FullName)"; Write-Host "Size: $([math]::Round($item.Length / 1MB,1)) MB"; Write-Host "SHA-256: $($hash.Hash)"
} catch { Write-Warning "Publish staging retained for diagnosis: $stage"; throw } finally {
  Remove-Item Env:\WSL_MANAGER_PUBLISH_DIR,Env:\WSL_MANAGER_INSTALLER_OUTPUT_DIR,Env:\WSL_MANAGER_REPOSITORY_ROOT -ErrorAction SilentlyContinue
  if ($built -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
