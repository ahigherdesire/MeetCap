; ============================================================================
;  MeetCap installer  (Inno Setup 6)
;  Build the app first:   powershell -File build\publish.ps1
;  Then compile this:     iscc installer\MeetCap.iss
; ============================================================================

#define AppName        "MeetCap"
#define AppVersion      "1.0.0"
#define AppPublisher    "MeetCap"
#define AppExeName      "MeetCap.exe"
#define PublishDir      "..\artifacts\publish"

[Setup]
AppId={{B7E5A4C2-9F3D-4A18-9C6E-4F8A1D2C3E40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=MeetCap-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; MeetCap installs per-user-visible but to Program Files; needs admin for that folder.
PrivilegesRequired=admin
MinVersion=10.0.19041
SetupIconFile=..\src\MeetCap\Assets\meetcap.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Ships the entire self-contained publish output (app + .NET runtime + native capture DLLs).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";            Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch MeetCap when the wizard finishes (starts minimized to the tray).
Filename: "{app}\{#AppExeName}"; Parameters: "--minimized"; \
  Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop any running instance before files are removed.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExeName} /F"; Flags: runhidden; RunOnceId: "KillMeetCap"

[UninstallDelete]
; Leave the user's recordings alone; only remove the app's own state folder is optional.
; (Recordings live in Videos\MeetCap by default and are never touched.)

[Registry]
; Clean up the per-user "start on boot" entry on uninstall (the app manages it while installed).
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueName: "MeetCap"; \
  ValueType: none; Flags: deletevalue uninsdeletevalue
