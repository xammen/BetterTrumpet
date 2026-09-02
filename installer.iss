; Architecture selection. Pass /DArch=x86|x64|arm64 to ISCC to build a specific
; architecture. Defaults to x86 for backward compatibility.
#ifndef Arch
  #define Arch "x86"
#endif

#define AppVersionStr "3.4.0"

#if Arch == "x86"
  #define BuildDir "Build\Release"
  #define ArchAllowed "x86compatible"
  #define ArchSuffix ""
#elif Arch == "x64"
  #define BuildDir "Build\Release-x64"
  #define ArchAllowed "x64compatible"
  #define ArchIn64BitMode "x64compatible"
  #define ArchSuffix "-x64"
#elif Arch == "arm64"
  #define BuildDir "Build\Release-arm64"
  #define ArchAllowed "arm64"
  #define ArchIn64BitMode "arm64"
  #define ArchSuffix "-arm64"
#else
  #error Unknown Arch value. Use x86, x64, or arm64.
#endif

#define OutputName "BetterTrumpet-" + AppVersionStr + "-setup" + ArchSuffix

[Setup]
; One AppId for all three architectures, matching the value Inno derived from AppName before it
; was set explicitly, so existing installs keep their uninstall entry.
;
; This makes a different architecture upgrade in place only for the default per-user install,
; where the uninstall key lives under HKCU (not subject to WOW64 redirection) and {autopf} is
; the same directory for every architecture. For an elevated all-users install it does not:
; the x86 build registers under HKLM\...\Wow6432Node and lands in "Program Files (x86)" while
; the native builds use the 64-bit view and "Program Files", so the two cannot see each other
; and would coexist. Switching architecture on an all-users install means uninstalling first.
AppId=BetterTrumpet
AppName=BetterTrumpet
AppVersion={#AppVersionStr}
AppVerName=BetterTrumpet {#AppVersionStr}
AppPublisher=xammen
AppPublisherURL=https://bettertrumpet.hiii.boo
AppSupportURL=https://github.com/xammen/BetterTrumpet/issues
AppUpdatesURL=https://github.com/xammen/BetterTrumpet/releases
DefaultDirName={autopf}\BetterTrumpet
DefaultGroupName=BetterTrumpet
UninstallDisplayIcon={app}\BetterTrumpet.exe
OutputDir=dist
OutputBaseFilename={#OutputName}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed={#ArchAllowed}
; Without this, {autopf} resolves through {commonpf32}, so a native x64/arm64 build would install
; into "Program Files (x86)" with its registry writes redirected into Wow6432Node.
; x86 omits the directive entirely: blank is both its default and the value it wants.
#ifdef ArchIn64BitMode
ArchitecturesInstallIn64BitMode={#ArchIn64BitMode}
#endif
SetupIconFile=EarTrumpet\Assets\BetterTrumpet.ico
WizardStyle=modern
WizardSizePercent=110,110
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
LicenseFile=LICENSE
VersionInfoVersion={#AppVersionStr}.0
VersionInfoCompany=xammen
VersionInfoDescription=BetterTrumpet - Windows Volume Control
VersionInfoProductName=BetterTrumpet
VersionInfoProductVersion={#AppVersionStr}
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
english.AddToPathTask=Add to PATH (enables "bt" command in terminal for CLI)
french.AddToPathTask=Ajouter au PATH (active la commande "bt" dans le terminal pour le CLI)
english.OptionsGroup=Options:
french.OptionsGroup=Options :
english.LaunchAfterInstall=Launch BetterTrumpet
french.LaunchAfterInstall=Lancer BetterTrumpet

[Files]
Source: "{#BuildDir}\BetterTrumpet.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bt.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\af-ZA\*"; DestDir: "{app}\af-ZA"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ar-SA\*"; DestDir: "{app}\ar-SA"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\bs-latn-ba\*"; DestDir: "{app}\bs-latn-ba"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ca-ES\*"; DestDir: "{app}\ca-ES"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\cs-CZ\*"; DestDir: "{app}\cs-CZ"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\da-DK\*"; DestDir: "{app}\da-DK"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\de-DE\*"; DestDir: "{app}\de-DE"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\el-GR\*"; DestDir: "{app}\el-GR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\es-ES\*"; DestDir: "{app}\es-ES"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\fi-FI\*"; DestDir: "{app}\fi-FI"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\fr-FR\*"; DestDir: "{app}\fr-FR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\he-IL\*"; DestDir: "{app}\he-IL"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\hr-HR\*"; DestDir: "{app}\hr-HR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\hu-HU\*"; DestDir: "{app}\hu-HU"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\it-IT\*"; DestDir: "{app}\it-IT"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ja-JP\*"; DestDir: "{app}\ja-JP"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ko-KR\*"; DestDir: "{app}\ko-KR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\nl-NL\*"; DestDir: "{app}\nl-NL"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\pl-PL\*"; DestDir: "{app}\pl-PL"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\pt-PT\*"; DestDir: "{app}\pt-PT"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ro-RO\*"; DestDir: "{app}\ro-RO"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ru-RU\*"; DestDir: "{app}\ru-RU"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\sl-SI\*"; DestDir: "{app}\sl-SI"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\sv-SE\*"; DestDir: "{app}\sv-SE"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\ta-IN\*"; DestDir: "{app}\ta-IN"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\th-TH\*"; DestDir: "{app}\th-TH"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\tr-TR\*"; DestDir: "{app}\tr-TR"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\uk-UA\*"; DestDir: "{app}\uk-UA"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\vi-VN\*"; DestDir: "{app}\vi-VN"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\zh-CN\*"; DestDir: "{app}\zh-CN"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\zh-TW\*"; DestDir: "{app}\zh-TW"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\SettingsWeb\*"; DestDir: "{app}\SettingsWeb"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\AnnouncementsWeb\*"; DestDir: "{app}\AnnouncementsWeb"; Flags: ignoreversion recursesubdirs
Source: "{#BuildDir}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs

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

// Remove from PATH + clean BetterTrumpet registry on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  OrigPath: string;
  AppDir: string;
  P: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove from PATH
    AppDir := ExpandConstant('{app}');
    if RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
    begin
      P := Pos(';' + Uppercase(AppDir), Uppercase(OrigPath));
      if P > 0 then
      begin
        Delete(OrigPath, P, Length(';' + AppDir));
        RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath);
      end;
    end;

    // Clean all BetterTrumpet settings (so reinstall triggers onboarding)
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\EarTrumpet');
  end;
end;
