# Installation

For the desktop preview, download the unsigned x64 Setup `.exe` from the [GitHub Releases page](https://github.com/geekedctrl/wsl-management-tool/releases), verify the checksum in the release notes, and run it for the current Windows user. The installer includes the desktop app and runtime; WSL itself must already be installed for distribution management.

For the PowerShell CLI or development files:

1. Obtain the source from the project's repository.
2. Extract it to a user-writable directory.
3. Open PowerShell 5.1 or PowerShell 7 in that directory.
4. Run `./WSL-Manager.ps1` or use the documented `-Action` parameters.

WSL Manager invokes the installed `wsl.exe` directly. It does not install WSL, enable Windows features, or request elevation automatically. A constrained execution policy may require a signed copy or an explicit process-scoped policy change.

## WinUI desktop preview

The desktop client is an unpackaged WinUI 3 application. Its Windows App SDK target minimum is Windows 10 build 17763, and it targets .NET 10. Running it requires the .NET 10 Desktop Runtime and the Windows App SDK 2.5.1 runtime for unpackaged applications. Microsoft limits .NET 10 support on Windows 10 to supported LTSC/Enterprise editions; Windows 10 runtime behavior has not been tested for this app. The app was tested on Windows 11 only. See Microsoft's [.NET on Windows support table](https://learn.microsoft.com/dotnet/core/install/windows), [Windows App SDK support matrix](https://learn.microsoft.com/windows/apps/windows-app-sdk/support), and [unpackaged deployment guide](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps) for requirements. These prerequisites are installed separately; WSL Manager does not bootstrap or install them. PowerShell 7 is preferred, with Windows PowerShell 5.1 as a fallback for the bundled PowerShell backend.

To build and launch from a source checkout on an x64 Windows development machine with the .NET 10 SDK:

```powershell
dotnet restore WSLManager.Desktop.sln -p:Platform=x64
dotnet build WSLManager.Desktop.sln -p:Platform=x64
dotnet run --project src/WSLManager.Desktop/WSLManager.Desktop.csproj -p:Platform=x64
```

The project bundles `WSL-Manager.ps1` and `src/Core/WSLManager.psm1` into the application output and invokes that backend as a separate PowerShell process. A WSL installation is not needed to open the UI; distribution operations show a diagnostic error if WSL is unavailable.

### Build the per-user Setup `.exe`

The installer uses Inno Setup 7 and bundles a self-contained .NET/Windows App SDK deployment. Install Inno Setup 7, then run this command from the repository root in PowerShell:

```powershell
./scripts/Build-Installer.ps1
```

The output is `artifacts/installer/WSL-Manager-Setup-1.0.0-x64.exe`. Installation defaults to `%LOCALAPPDATA%\Programs\WSL Manager` and does not require elevation. The installer creates a Start Menu shortcut and optionally a desktop shortcut. Uninstall removes only the application install folder and shortcuts; per-user WSL Manager settings and logs are preserved. It never installs, stops, or changes WSL distributions or Windows features.

This setup is unsigned. Public distribution may trigger Windows SmartScreen or an unknown-publisher warning until the installer is signed with a trusted code-signing certificate. The package contains the Windows App SDK self-contained runtime, increasing its size; the project must ship an updated installer when servicing that runtime. There is no automatic update mechanism.
