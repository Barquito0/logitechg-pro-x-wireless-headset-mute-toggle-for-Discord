#define AppVersion "1.0.1"
#define AppName "Headset Button Bridge"
[Setup]
AppId={{52E9D6A8-E41D-4D39-A0BB-0BAF4B23EA6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Barquito0
AppPublisherURL=https://github.com/Barquito0/logitechg-pro-x-wireless-headset-mute-toggle-for-Discord
AppSupportURL=https://github.com/Barquito0/logitechg-pro-x-wireless-headset-mute-toggle-for-Discord/issues
DefaultDirName={localappdata}\Programs\Headset Button Bridge
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=HeadsetButtonBridge-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\HeadsetButtonBridge.exe
CloseApplications=yes
CloseApplicationsFilter=HeadsetButtonBridge.exe
RestartApplications=no
LicenseFile=..\LICENSE
SetupLogging=no

[Files]
Source: "..\dist\HeadsetButtonBridge.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\HeadsetButtonBridge.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\HeadsetButtonBridge.exe"; Parameters: "--setup"

[Run]
Filename: "{app}\HeadsetButtonBridge.exe"; Parameters: "--setup"; Description: "Calibrate the headset button and choose a shortcut"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\HeadsetButtonBridge.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "StopBridge"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "HeadsetButtonBridge"; Flags: uninsdeletevalue

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('This app requires Microsoft .NET Framework 4.8, included with supported Windows 10 and Windows 11 installations. Install Windows updates, then try again.', mbError, MB_OK);
end;
