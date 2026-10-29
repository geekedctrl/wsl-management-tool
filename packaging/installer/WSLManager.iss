#define AppName "WSL Manager"
#define AppVersion "1.0.0"
#define PublishDir GetEnv("WSL_MANAGER_PUBLISH_DIR")
#define InstallerOutputDir GetEnv("WSL_MANAGER_INSTALLER_OUTPUT_DIR")
#define RepositoryRoot GetEnv("WSL_MANAGER_REPOSITORY_ROOT")

#if PublishDir == ""
  #error "WSL_MANAGER_PUBLISH_DIR is required. Run scripts/Build-Installer.ps1."
#endif
#if InstallerOutputDir == ""
  #error "WSL_MANAGER_INSTALLER_OUTPUT_DIR is required. Run scripts/Build-Installer.ps1."
#endif
#if RepositoryRoot == ""
  #error "WSL_MANAGER_REPOSITORY_ROOT is required. Run scripts/Build-Installer.ps1."
#endif

[Setup]
AppId={{C4797B03-97E2-46F9-B12A-BBAA1BC7B886}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=WSL Manager contributors
AppPublisherURL=https://github.com/geekedctrl/wsl-management-tool
AppSupportURL=https://github.com/geekedctrl/wsl-management-tool/issues
AppUpdatesURL=https://github.com/geekedctrl/wsl-management-tool
DefaultDirName={localappdata}\Programs\WSL Manager
DefaultGroupName=WSL Manager
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=WSL Manager
UninstallDisplayIcon={app}\WSLManager.Desktop.exe
LicenseFile={#RepositoryRoot}\LICENSE
OutputDir={#InstallerOutputDir}
OutputBaseFilename=WSL-Manager-Setup-1.0.0-x64
VersionInfoVersion=1.0.0.0
VersionInfoCompany=WSL Manager contributors
VersionInfoDescription=WSL Manager per-user setup
VersionInfoProductName=WSL Manager
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\WSL Manager"; Filename: "{app}\WSLManager.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\WSL Manager"; Filename: "{app}\WSLManager.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\WSLManager.Desktop.exe"; Description: "Launch WSL Manager"; Flags: postinstall nowait skipifsilent
