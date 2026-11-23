; DeskVerve Inno Setup 6 Script
; Copyright (c) 2026 Quantum Step (https://quantumstep.in)

#ifndef MyAppVersion
#define MyAppVersion "3.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\dist\QuantumUpdater-WinUI"
#endif

#ifndef OutputDir
#define OutputDir "..\dist\release"
#endif

[Setup]
AppId={{4E32B66C-5B4B-4E7B-9A39-4E6D2543F9A1}
AppName=DeskVerve
AppVersion={#MyAppVersion}
AppPublisher=Quantum Step
AppPublisherURL=https://quantumstep.in
AppSupportURL=https://github.com/merchanthasan2/AudoChocolatey/issues
AppUpdatesURL=https://github.com/merchanthasan2/AudoChocolatey/releases
DefaultDirName={autopf}\DeskVerve
DefaultGroupName=DeskVerve
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\brand\desk-verve-icon.ico
UninstallDisplayName=DeskVerve
UninstallDisplayIcon={app}\desk-verve-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=DeskVerve-Setup-{#MyAppVersion}-x64
CloseApplications=yes
CloseApplicationsFilter=DeskVerve.exe,QuantumUpdater.exe
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\brand\desk-verve-icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\DeskVerve"; Filename: "{app}\DeskVerve.exe"; IconFilename: "{app}\desk-verve-icon.ico"
Name: "{autodesktop}\DeskVerve"; Filename: "{app}\DeskVerve.exe"; IconFilename: "{app}\desk-verve-icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\DeskVerve.exe"; Description: "{cm:LaunchProgram,DeskVerve}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c schtasks /Delete /TN ""\Quantum Step\DeskVerve Start"" /F >nul 2>&1"; Flags: runhidden

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Terminate any running DeskVerve or legacy QuantumUpdater process before installation
  Exec(ExpandConstant('{cmd}'), '/c taskkill /F /IM DeskVerve.exe /T >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{cmd}'), '/c taskkill /F /IM QuantumUpdater.exe /T >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{cmd}'), '/c schtasks /Delete /TN "\Quantum Step\DeskVerve Start" /F >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
