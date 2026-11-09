; EXE installer for TeamViewer Cleanup Tool (Inno Setup 6).
; Built by build.ps1, which passes /DAppVersion, /DAppDir, /DRepoRoot and /DOutDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6F3C2B1E-6C1D-4E1B-9A57-4B2D6A1E9C30}
AppName=TeamViewer Cleanup Tool
AppVersion={#AppVersion}
AppVerName=TeamViewer Cleanup Tool {#AppVersion}
AppPublisher=RatioJuris
AppPublisherURL=https://github.com/RatioJuris/TeamViewer-Cleanup-Tool
AppSupportURL=https://ratiojuris.github.io/legal/teamviewer-cleanup-tool/support/
AppUpdatesURL=https://github.com/RatioJuris/TeamViewer-Cleanup-Tool/releases
VersionInfoVersion={#AppVersion}
VersionInfoCompany=RatioJuris
VersionInfoDescription=TeamViewer Cleanup Tool Setup
DefaultDirName={autopf}\TeamViewer Cleanup Tool
DefaultGroupName=TeamViewer Cleanup Tool
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible arm64
MinVersion=10.0
LicenseFile={#RepoRoot}\LICENSE
SetupIconFile={#RepoRoot}\src\TeamViewerCleanup\app.ico
UninstallDisplayIcon={app}\TeamViewerCleanupTool.exe
UninstallDisplayName=TeamViewer Cleanup Tool
OutputDir={#OutDir}
OutputBaseFilename=TeamViewerCleanupTool-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppDir}\TeamViewerCleanupTool.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppDir}\TeamViewerCleanupTool.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\TeamViewer Cleanup Tool"; Filename: "{app}\TeamViewerCleanupTool.exe"
Name: "{autodesktop}\TeamViewer Cleanup Tool"; Filename: "{app}\TeamViewerCleanupTool.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TeamViewerCleanupTool.exe"; Description: "{cm:LaunchProgram,TeamViewer Cleanup Tool}"; Flags: nowait postinstall skipifsilent
