#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "Chaos Overlords: New Chrome"
#define MyAppGroupName "Chaos Overlords - New Chrome"
#define MyAppShortcutName "Chaos Overlords - New Chrome"
#define MyAppPublisher "kibertoad"
#define MyAppExeName "Rechaos.Game.exe"
#define PackageRoot "..\..\artifacts\ChaosOverlordsNewChrome-win-x64"

[Setup]
AppId={{9B28D38D-670C-45DD-B9D6-F7BD2680523B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\ChaosOverlordsNewChrome
DisableDirPage=no
DefaultGroupName={#MyAppGroupName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
OutputDir=..\..\artifacts
OutputBaseFilename=ChaosOverlords-NewChrome-Setup-{#MyAppVersion}
UninstallDisplayIcon={app}\Game\{#MyAppExeName}
SetupLogging=yes

[Files]
Source: "{#PackageRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppShortcutName}"; Filename: "{app}\Game\{#MyAppExeName}"; WorkingDir: "{app}\Game"
Name: "{autodesktop}\{#MyAppShortcutName}"; Filename: "{app}\Game\{#MyAppExeName}"; WorkingDir: "{app}\Game"; Tasks: desktopicon
Name: "{group}\Import Assets from Original Chaos Overlords"; Filename: "{app}\Install Original Resources.bat"; WorkingDir: "{app}"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Game\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; WorkingDir: "{app}\Game"; Flags: postinstall nowait skipifsilent unchecked

[Code]
const
  PurchaseUrl = 'https://www.gog.com/en/game/chaos_overlords';

var
  OriginalPage: TInputDirWizardPage;
  ImportCheckBox: TNewCheckBox;
  PurchaseButton: TNewButton;
  DetectedOriginalPath: String;
  ImportOutput: String;
  ImportFailed: Boolean;

function IsOriginalInstall(const Candidate: String): Boolean;
var
  DataPath: String;
begin
  DataPath := AddBackslash(Candidate) + 'DATA\';
  Result := (Candidate <> '') and
    DirExists(DataPath + 'PX16') and
    FileExists(DataPath + 'SITES') and
    FileExists(DataPath + 'GANGS') and
    FileExists(DataPath + 'ITEMS');
end;

function SearchUninstallRecords(const RootKey: Integer; var Found: String): Boolean;
var
  BaseKey, DisplayName, Candidate: String;
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  BaseKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  if not RegGetSubkeyNames(RootKey, BaseKey, Names) then exit;
  for I := 0 to GetArrayLength(Names) - 1 do
    if RegQueryStringValue(RootKey, BaseKey + '\' + Names[I], 'DisplayName', DisplayName) and
       (Pos('CHAOS OVERLORDS', Uppercase(DisplayName)) > 0) and
       RegQueryStringValue(RootKey, BaseKey + '\' + Names[I], 'InstallLocation', Candidate) and
       IsOriginalInstall(RemoveQuotes(Candidate)) then
    begin
      Found := RemoveQuotes(Candidate);
      Result := True;
      exit;
    end;
end;

function SearchGogRecords(const RootKey: Integer; var Found: String): Boolean;
var
  BaseKey, Candidate: String;
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  BaseKey := 'Software\GOG.com\Games';
  if not RegGetSubkeyNames(RootKey, BaseKey, Names) then exit;
  for I := 0 to GetArrayLength(Names) - 1 do
    if RegQueryStringValue(RootKey, BaseKey + '\' + Names[I], 'PATH', Candidate) and
       IsOriginalInstall(RemoveQuotes(Candidate)) then
    begin
      Found := RemoveQuotes(Candidate);
      Result := True;
      exit;
    end;
end;

function FindOriginalInstall: String;
var
  Candidate: String;
  DriveNumber: Integer;
begin
  if ExpandConstant('{param:NOIMPORT|0}') = '1' then
  begin
    Result := '';
    exit;
  end;
  Result := ExpandConstant('{param:ORIGINAL|}');
  if IsOriginalInstall(Result) then exit;
  Result := '';

  if SearchGogRecords(HKCU32, Result) or SearchGogRecords(HKLM32, Result) or
     SearchGogRecords(HKCU64, Result) or SearchGogRecords(HKLM64, Result) or
     SearchUninstallRecords(HKCU32, Result) or SearchUninstallRecords(HKLM32, Result) or
     SearchUninstallRecords(HKCU64, Result) or SearchUninstallRecords(HKLM64, Result) then
    exit;

  for DriveNumber := 67 to 90 do
  begin
    Candidate := Chr(DriveNumber) + ':\GOG Games\Chaos Overlords';
    if IsOriginalInstall(Candidate) then begin Result := Candidate; exit; end;
    Candidate := Chr(DriveNumber) + ':\Program Files (x86)\GOG Galaxy\Games\Chaos Overlords';
    if IsOriginalInstall(Candidate) then begin Result := Candidate; exit; end;
    Candidate := Chr(DriveNumber) + ':\Program Files\GOG Galaxy\Games\Chaos Overlords';
    if IsOriginalInstall(Candidate) then begin Result := Candidate; exit; end;
  end;
end;

procedure OpenPurchasePage(Sender: TObject);
var
  ErrorCode: Integer;
begin
  if not ShellExec('', PurchaseUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode) then
    MsgBox('Windows could not open the GOG purchase page: ' + SysErrorMessage(ErrorCode), mbError, MB_OK);
end;

procedure InitializeWizard;
begin
  DetectedOriginalPath := FindOriginalInstall;
  OriginalPage := CreateInputDirPage(wpSelectDir,
    'Assets from the original Chaos Overlords',
    'Import art, music, sound, video, and other game assets from your legally owned copy.',
    'If you own Chaos Overlords, install it before running this installer. Setup can copy and convert the ' +
    'required game assets without modifying your original installation. Select that installation below, ' +
    'or clear the import option if you do not have it installed yet.',
    False, '');
  OriginalPage.Add('GOG installation folder:');
  if DetectedOriginalPath <> '' then
    OriginalPage.Values[0] := DetectedOriginalPath
  else
    OriginalPage.Values[0] := ExpandConstant('{sd}\GOG Games\Chaos Overlords');

  ImportCheckBox := TNewCheckBox.Create(OriginalPage);
  ImportCheckBox.Parent := OriginalPage.Surface;
  ImportCheckBox.Left := OriginalPage.Edits[0].Left;
  ImportCheckBox.Top := OriginalPage.Edits[0].Top + OriginalPage.Edits[0].Height + ScaleY(20);
  ImportCheckBox.Width := OriginalPage.SurfaceWidth;
  ImportCheckBox.Caption := 'Import art, music, sound, video, and other assets from my legal Chaos Overlords copy';
  ImportCheckBox.Checked := ExpandConstant('{param:NOIMPORT|0}') <> '1';

  PurchaseButton := TNewButton.Create(OriginalPage);
  PurchaseButton.Parent := OriginalPage.Surface;
  PurchaseButton.Left := OriginalPage.Edits[0].Left;
  PurchaseButton.Top := ImportCheckBox.Top + ImportCheckBox.Height + ScaleY(18);
  PurchaseButton.Width := ScaleX(210);
  PurchaseButton.Caption := 'Buy a legal copy on GOG';
  PurchaseButton.OnClick := @OpenPurchasePage;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = OriginalPage.ID) and ImportCheckBox.Checked and
     not IsOriginalInstall(OriginalPage.Values[0]) then
  begin
    MsgBox('That folder is not a complete Chaos Overlords installation. Select the folder containing ' +
      'DATA\PX16, DATA\SITES, DATA\GANGS, and DATA\ITEMS. If you own the game, install it before ' +
      'running this installer; otherwise clear the asset import option.', mbError, MB_OK);
    Result := False;
  end;
end;

function ShouldImportOriginal: Boolean;
begin
  if WizardSilent then
    Result := IsOriginalInstall(DetectedOriginalPath)
  else
    Result := ImportCheckBox.Checked and IsOriginalInstall(OriginalPage.Values[0]);
end;

function SelectedOriginalPath: String;
begin
  if WizardSilent then Result := DetectedOriginalPath
  else Result := OriginalPage.Values[0];
end;

procedure ImportLogLine(const S: String; const Error, FirstLine: Boolean);
var
  Line: String;
begin
  Line := Trim(S);
  if Line = '' then exit;
  if Error then Line := 'ERROR: ' + Line;
  Log('Asset importer: ' + Line);
  ImportOutput := ImportOutput + Line + #13#10;
  if Length(ImportOutput) > 12000 then
    Delete(ImportOutput, 1, Length(ImportOutput) - 12000);
  WizardForm.StatusLabel.Caption := Line;
end;

function RunAssetImport(const Source: String; var Failure: String): Boolean;
var
  ResultCode: Integer;
  Extractor, Parameters: String;
  Started: Boolean;
begin
  Extractor := ExpandConstant('{app}\Tools\Rechaos.Extractor.exe');
  Parameters := '--source "' + Source + '" --output "' +
    ExpandConstant('{app}\Game\Assets') + '"';
  ImportOutput := '';
  Failure := '';
  ResultCode := -1;
  WizardForm.StatusLabel.Caption := 'Importing assets from your legal Chaos Overlords copy...';
  try
    Started := ExecAndLogOutput(Extractor, Parameters, ExpandConstant('{app}'), SW_HIDE,
      ewWaitUntilTerminated, ResultCode, @ImportLogLine);
  except
    Started := False;
    Failure := 'The asset importer could not be started: ' + GetExceptionMessage;
  end;
  if not Started and (Failure = '') then
    Failure := 'The asset importer could not be started.';
  if Started and (ResultCode <> 0) then
    Failure := 'Asset import failed with error ' + IntToStr(ResultCode) + '.' + #13#10#13#10 +
      ImportOutput;
  if Started and (ResultCode = 0) and
     not FileExists(ExpandConstant('{app}\Game\Assets\manifest.json')) then
    Failure := 'Asset import reported success, but Game\Assets\manifest.json was not created.';
  Result := Started and (ResultCode = 0) and (Failure = '');
end;

procedure RaiseImportFailure(const Failure: String);
begin
  ImportFailed := True;
  RaiseException(Failure);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Source, Failure: String;
begin
  if (CurStep <> ssPostInstall) or not ShouldImportOriginal then exit;

  Source := SelectedOriginalPath;
  while not RunAssetImport(Source, Failure) do
  begin
    if WizardSilent then
      RaiseImportFailure(Failure);
    if MsgBox(Failure + #13#10#13#10 +
       'Choose Retry to select another installed copy of Chaos Overlords, or Cancel to stop setup.',
       mbError, MB_RETRYCANCEL) <> IDRETRY then
      RaiseImportFailure(Failure);

    repeat
      if not BrowseForFolder('Select the folder where Chaos Overlords is installed:', Source, False) then
        RaiseImportFailure('Asset import failed and no replacement Chaos Overlords installation was selected.');
      if not IsOriginalInstall(Source) then
        MsgBox('That folder does not contain DATA\PX16, DATA\SITES, DATA\GANGS, and DATA\ITEMS. ' +
          'Select the installed Chaos Overlords folder.', mbError, MB_OK);
    until IsOriginalInstall(Source);
  end;
  WizardForm.StatusLabel.Caption := 'Original Chaos Overlords assets imported and verified.';
end;

function GetCustomSetupExitCode: Integer;
begin
  if ImportFailed then Result := 10
  else Result := 0;
end;
