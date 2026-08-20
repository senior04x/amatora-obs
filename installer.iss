; =========================================================================
; AMATORA OBS CONTROLLER — Professional Inno Setup Script
; =========================================================================

#define MyAppName "AMATORA OBS Controller"
#define MyAppVersion "3.5.0"
#define MyAppPublisher "AMATORA Group"
#define MyAppURL "https://amatora.uz"
#define MyAppExeName "AMATORA.exe"
#define MyAppIcon "app.ico"

[Setup]
; App Identity
AppId={{E8B62B44-90C1-4D95-A759-9E6D7E84B1F8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Destination Folder & Start Menu
DefaultDirName={autopf}\AMATORA\OBS Controller
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Output Configuration
OutputDir=dist
OutputBaseFilename=AMATORA OBS Controller Setup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppIcon}

; Modern UI & Compression
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes

; Permissions & Running Instance Management
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Ish stolida yorliq (Desktop Shortcut) yaratish"; GroupDescription: "Qo'shimcha:"; Flags: checkedonce
Name: "autostart"; Description: "Kompyuter yoqilganda avtomatik ishga tushirish"; GroupDescription: "Qo'shimcha:"; Flags: unchecked

[Files]
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppIcon}"; DestDir: "{app}"; Flags: ignoreversion
Source: "logo.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "amatora_stinger (2).webm"; DestDir: "{app}"; Flags: ignoreversion
Source: "OBS-template.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "obs_latest_replay.lua"; DestDir: "{app}"; Flags: ignoreversion
Source: "replay_badge.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "stinger_transition.html"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"
Name: "{group}\Dasturni o'chirish (Uninstall)"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} dasturini ishga tushirish"; Flags: nowait postinstall skipifsilent
