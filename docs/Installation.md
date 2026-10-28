# Installation

1. Obtain the source from the project's repository or a release archive.
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

The project bundles `WSL-Manager.ps1` and `src/Core/WSLManager.psm1` into the application output and invokes that backend as a separate PowerShell process. There is no MSI/MSIX installer in this preview. A WSL installation is not needed to open the UI; distribution operations show a diagnostic error if WSL is unavailable.
