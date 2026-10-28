#define AppName "lidless"
#define AppVersion "0.2.4"
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
[Files]
Source: "..\dist\lidless.exe"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\lidless"; Filename: "{app}\lidless.exe"
Name: "{autodesktop}\lidless"; Filename: "{app}\lidless.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"
[Run]
Filename: "{app}\lidless.exe"; Description: "Launch lidless"; Flags: nowait postinstall skipifsilent
