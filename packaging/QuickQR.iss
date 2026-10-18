; Candidate installer source. Not uploaded by the Release workflow until install/uninstall QA is complete.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDirectory
  #error Supply /DPublishDirectory with a tested win-x64 self-contained publish directory.
#endif
#ifndef OutputDirectory
  #define OutputDirectory "..\artifacts\installer-unverified"
#endif

[Setup]
AppId={{347DB042-387C-4A43-9883-24DE9EE1439B}
AppName=QuickQR for Windows
AppVersion={#AppVersion}
AppPublisher=you2032zhu
AppPublisherURL=https://github.com/you2032zhu/QuickQR
DefaultDirName={localappdata}\Programs\QuickQR
DefaultGroupName=QuickQR
DisableDirPage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#OutputDirectory}
OutputBaseFilename=QuickQR-Setup-v{#AppVersion}-x64
SetupIconFile=..\QuickQR.Windows\Resources\app.ico
UninstallDisplayIcon={app}\QuickQR.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\QuickQR.Windows.1.Instance
CloseApplications=no
RestartApplications=no

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\QuickQR"; Filename: "{app}\QuickQR.exe"
Name: "{autodesktop}\QuickQR"; Filename: "{app}\QuickQR.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\QuickQR.exe"; Description: "Open QuickQR"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExistingCommand: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Remove only this installation's login entry. Preserve all user settings/data. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'QuickQR', ExistingCommand) then
      if CompareText(ExistingCommand, '"' + ExpandConstant('{app}\QuickQR.exe') + '" --login') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'QuickQR');
  end;
end;
