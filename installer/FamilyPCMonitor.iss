; Inno Setup 6 script. First publish the app as documented in README.md.
[Setup]
AppId={{F5925013-21E5-46D1-9CB1-8C700CB915E2}
AppName=Family PC Monitor
AppVersion=1.0.0
DefaultDirName={autopf}\Family PC Monitor
DefaultGroupName=Family PC Monitor
OutputDir=output
OutputBaseFilename=FamilyPCMonitorSetup
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\FamilyPCMonitor.exe

[Files]
Source: "..\src\FamilyPCMonitor.App\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Family PC Monitor"; Filename: "{app}\FamilyPCMonitor.exe"
Name: "{autodesktop}\Family PC Monitor"; Filename: "{app}\FamilyPCMonitor.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\FamilyPCMonitor.exe"; Description: "Launch Family PC Monitor"; Flags: postinstall nowait skipifsilent
