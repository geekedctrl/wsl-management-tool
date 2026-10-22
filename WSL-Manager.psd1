@{
    RootModule = 'src/Core/WSLManager.psm1'
    ModuleVersion = '1.0.0'
    GUID = '517fd2df-9560-47ef-99b8-7ff880f6543b'
    Author = 'WSL Manager contributors'
    Description = 'Safe command-line and terminal management for Windows Subsystem for Linux.'
    PowerShellVersion = '5.1'
    FunctionsToExport = @('Get-WslDistributions','Get-WslRunningNames','Get-WslDistro','Get-WslDistroDetails','Get-WslCapabilities','Get-ManagerDiagnostics','Backup-WslDistribution','Restore-WslBackup','Invoke-DistributionAction','Move-WslDistribution','Copy-WslDistribution','Resize-WslDistribution')
    PrivateData = @{ PSData = @{ Tags = @('WSL','Linux','Management'); LicenseUri='https://opensource.org/license/mit' } }
}
