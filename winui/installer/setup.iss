; Inno Setup script for the unpackaged WinUI 3 widget.
; CI runs: iscc //DAppVersion=<version> winui/installer/setup.iss
; after `dotnet publish -c Release -r win-x64 --self-contained -o winui/publish`.

#define AppName "Codex Usage Widget"
#define AppPublisher "wahackr"
#define AppExe "CodexUsageWidget.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
; Stable identity, so a reinstall replaces the same "Apps & features" entry
; instead of leaving an orphan behind.
AppId={{8D3C6A52-9E1B-4C77-9F45-2A6B0D7E1C39}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Codex Usage Widget
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=CodexUsageWidget-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
; The widget lives in the tray, so an upgrade has to be able to close it.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; Settings under %LOCALAPPDATA%\CodexUsageWidget are deliberately left alone by
; the uninstaller, the way Electron leaves userData in place.
