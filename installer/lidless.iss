#define AppName "lidless"
#define AppVersion "0.4.3"
[Setup]
AppId={{C9C8C99D-80CB-4AA6-A17B-94442C98CF78}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\lidless
DefaultGroupName=lidless
OutputDir=..\dist
OutputBaseFilename=lidless-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\lidless.exe
LicenseFile=..\LICENSE
[Files]
Source: "..\dist\lidless.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSES\LibreHardwareMonitor-MPL-2.0.txt"; DestDir: "{app}\LICENSES"; Flags: ignoreversion
Source: "..\LICENSES\lidless-through-v0.3.0-MIT.txt"; DestDir: "{app}\LICENSES"; Flags: ignoreversion
[Icons]
Name: "{group}\lidless"; Filename: "{app}\lidless.exe"
Name: "{autodesktop}\lidless"; Filename: "{app}\lidless.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"
[Run]
Filename: "{app}\lidless.exe"; Description: "Launch lidless"; Flags: nowait postinstall skipifsilent
