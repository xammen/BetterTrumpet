[Setup]
AppName=BetterTrumpet
AppVersion=3.4.0
AppVerName=BetterTrumpet 3.4.0
AppPublisher=xammen
AppPublisherURL=https://bettertrumpet.hiii.boo
AppSupportURL=https://github.com/xammen/BetterTrumpet/issues
AppUpdatesURL=https://github.com/xammen/BetterTrumpet/releases
DefaultDirName={autopf}\BetterTrumpet
DefaultGroupName=BetterTrumpet
UninstallDisplayIcon={app}\BetterTrumpet.exe
OutputDir=dist
OutputBaseFilename=BetterTrumpet-3.4.0-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x86compatible
SetupIconFile=EarTrumpet\Assets\BetterTrumpet.ico
WizardStyle=modern
WizardSizePercent=110,110
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
LicenseFile=LICENSE
VersionInfoVersion=3.4.0.0
VersionInfoCompany=xammen
VersionInfoDescription=BetterTrumpet - Windows Volume Control
VersionInfoProductName=BetterTrumpet
VersionInfoProductVersion=3.4.0
MinVersion=10.0.17134

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:OptionsGroup}"
Name: "addtopath"; Description: "{cm:AddToPathTask}"; GroupDescription: "{cm:OptionsGroup}"; Flags: checkedonce

[CustomMessages]
english.StartupTask=Launch BetterTrumpet at Windows startup
french.StartupTask=Lancer BetterTrumpet au demarrage de Windows
english.AddToPathTask=Add the "bt" command-line tool to PATH (control volume, devices and presets from any terminal, e.g. "bt help" - see docs/CLI.md on GitHub)
french.AddToPathTask=Ajouter l'outil en ligne de commande "bt" au PATH (controler volume, peripheriques et presets depuis n'importe quel terminal, ex. "bt help" - voir docs/CLI.md sur GitHub)
english.AddToPathAlreadyTask=Already added to PATH by a previous install ("bt" command-line tool) - uncheck to remove it
french.AddToPathAlreadyTask=Deja ajoute au PATH par une installation precedente (outil "bt" en ligne de commande) - decochez pour le retirer
english.OptionsGroup=Options:
french.OptionsGroup=Options :
english.LaunchAfterInstall=Launch BetterTrumpet
french.LaunchAfterInstall=Lancer BetterTrumpet

[Files]
Source: "Build\Release\BetterTrumpet.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Build\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Build\Release\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bt.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "Build\Release\af-ZA\*"; DestDir: "{app}\af-ZA"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ar-SA\*"; DestDir: "{app}\ar-SA"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\bs-latn-ba\*"; DestDir: "{app}\bs-latn-ba"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ca-ES\*"; DestDir: "{app}\ca-ES"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\cs-CZ\*"; DestDir: "{app}\cs-CZ"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\da-DK\*"; DestDir: "{app}\da-DK"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\de-DE\*"; DestDir: "{app}\de-DE"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\el-GR\*"; DestDir: "{app}\el-GR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\es-ES\*"; DestDir: "{app}\es-ES"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\fi-FI\*"; DestDir: "{app}\fi-FI"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\fr-FR\*"; DestDir: "{app}\fr-FR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\he-IL\*"; DestDir: "{app}\he-IL"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\hr-HR\*"; DestDir: "{app}\hr-HR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\hu-HU\*"; DestDir: "{app}\hu-HU"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\it-IT\*"; DestDir: "{app}\it-IT"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ja-JP\*"; DestDir: "{app}\ja-JP"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ko-KR\*"; DestDir: "{app}\ko-KR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\nl-NL\*"; DestDir: "{app}\nl-NL"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\pl-PL\*"; DestDir: "{app}\pl-PL"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\pt-PT\*"; DestDir: "{app}\pt-PT"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ro-RO\*"; DestDir: "{app}\ro-RO"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ru-RU\*"; DestDir: "{app}\ru-RU"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\sl-SI\*"; DestDir: "{app}\sl-SI"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\sv-SE\*"; DestDir: "{app}\sv-SE"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\ta-IN\*"; DestDir: "{app}\ta-IN"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\th-TH\*"; DestDir: "{app}\th-TH"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\tr-TR\*"; DestDir: "{app}\tr-TR"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\uk-UA\*"; DestDir: "{app}\uk-UA"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\vi-VN\*"; DestDir: "{app}\vi-VN"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\zh-CN\*"; DestDir: "{app}\zh-CN"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\zh-TW\*"; DestDir: "{app}\zh-TW"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\SettingsWeb\*"; DestDir: "{app}\SettingsWeb"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\AnnouncementsWeb\*"; DestDir: "{app}\AnnouncementsWeb"; Flags: ignoreversion recursesubdirs
Source: "Build\Release\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\BetterTrumpet"; Filename: "{app}\BetterTrumpet.exe"
Name: "{group}\Uninstall BetterTrumpet"; Filename: "{uninstallexe}"
Name: "{autodesktop}\BetterTrumpet"; Filename: "{app}\BetterTrumpet.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BetterTrumpet"; ValueData: """{app}\BetterTrumpet.exe"""; Flags: uninsdeletevalue; Tasks: startup
; Add install dir to user PATH so "bt" and "bettertrumpet" work from any terminal
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath(ExpandConstant('{app}')); Tasks: addtopath

[Run]
; After normal install: checkbox "Launch BetterTrumpet" (skipped in silent mode)
Filename: "{app}\BetterTrumpet.exe"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent
; After silent/verysilent install: always relaunch (no checkbox)
Filename: "{app}\BetterTrumpet.exe"; Flags: nowait postinstall skipifnotsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM BetterTrumpet.exe"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
// Kill running instance before install
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec('taskkill', '/F /IM BetterTrumpet.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

// Check if a directory is already in the user PATH
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  // Look for the path with leading and trailing semicolons
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;

// Remove a directory from the user PATH (keeps the REG_EXPAND_SZ type). Returns True if it was there.
function RemoveDirFromPath(Dir: string): Boolean;
var
  OrigPath: string;
  P: Integer;
begin
  Result := False;
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
    exit;
  // Wrap in semicolons so the first/last entries match like any other.
  OrigPath := ';' + OrigPath + ';';
  P := Pos(';' + Uppercase(Dir) + ';', Uppercase(OrigPath));
  if P = 0 then
    exit;
  Delete(OrigPath, P, Length(Dir) + 1);
  Delete(OrigPath, 1, 1);
  if (Length(OrigPath) > 0) and (OrigPath[Length(OrigPath)] = ';') then
    Delete(OrigPath, Length(OrigPath), 1);
  RegWriteExpandStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath);
  Result := True;
end;

var
  PathTaskInitialised: Boolean;
  PathWasAlreadyPresent: Boolean;

// "Add to PATH" task: when the folder is already in PATH, show it checked with an explanation
// instead of greying it out. Unchecking it removes the entry (see CurStepChanged).
procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
  TaskCaption: string;
  ItemCaption: string;
begin
  if (CurPageID = wpSelectTasks) and not PathTaskInitialised then
  begin
    PathTaskInitialised := True;
    if not NeedsAddPath(ExpandConstant('{app}')) then
    begin
      TaskCaption := CustomMessage('AddToPathTask');
      I := 0;
      while True do
      begin
        try
          ItemCaption := WizardForm.TasksList.ItemCaption[I];
        except
          break;
        end;
        if Pos(TaskCaption, ItemCaption) > 0 then
        begin
          PathWasAlreadyPresent := True;
          WizardForm.TasksList.ItemCaption[I] := CustomMessage('AddToPathAlreadyTask');
          WizardForm.TasksList.CheckItem(I, coCheck);
          break;
        end;
        I := I + 1;
      end;
    end;
  end;
end;

// Only act when the user saw the checkbox pre-checked and cleared it: silent installs
// (winget, Chocolatey, auto-update) never show the page and must leave PATH alone.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and PathWasAlreadyPresent and not WizardIsTaskSelected('addtopath') then
    RemoveDirFromPath(ExpandConstant('{app}'));
end;

// Remove from PATH + clean BetterTrumpet registry on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveDirFromPath(ExpandConstant('{app}'));

    // Clean all BetterTrumpet settings (so reinstall triggers onboarding)
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\EarTrumpet');
  end;
end;
