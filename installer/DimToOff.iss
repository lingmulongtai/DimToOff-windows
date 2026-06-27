#define AppName "DimToOff"
#ifndef AppVersion
#define AppVersion "0.0.0-dev"
#endif
#ifndef PackageVersion
#define PackageVersion AppVersion
#endif
#ifndef SourceDir
#define SourceDir "..\publish\win-x64-standalone"
#endif
#ifndef OutputDir
#define OutputDir "..\release\installer"
#endif

[Setup]
AppId={{D1E2F9B1-7B74-4D30-8D17-7E7F2F1D5104}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=lingmulongtai
AppPublisherURL=https://github.com/lingmulongtai/DimToOff-windows
AppSupportURL=https://github.com/lingmulongtai/DimToOff-windows/issues
AppUpdatesURL=https://github.com/lingmulongtai/DimToOff-windows/releases
DefaultDirName={localappdata}\Programs\DimToOff
DefaultGroupName=DimToOff
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=DimToOff-{#PackageVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayName=DimToOff
UninstallDisplayIcon={app}\DimToOff.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DimToOff"; Filename: "{app}\DimToOff.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\DimToOff"; Filename: "{app}\DimToOff.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DimToOff.exe"; Description: "Launch DimToOff"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "DimToOff"; Flags: uninsdeletevalue
