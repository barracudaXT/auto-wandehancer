; WandEnhancer installer with one-click auto-patch enablement.
; Build from the repo root with:
;   iscc src\installer\WandEnhancer.iss /DOutputDir=src\WandEnhancer\bin\Release

#define MyAppName "WandEnhancer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WandEnhancer Contributors"
#define MyAppURL "https://github.com/k1tbyte/Wand-Enhancer"
#define MyAppExeName "WandEnhancer.exe"

#ifndef OutputDir
  #define OutputDir "..\WandEnhancer\bin\Release"
#endif

[Setup]
AppId={{B8A7F4D2-9E1C-4A3B-8F6D-7C9E0A1B2D3F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.md
PrivilegesRequired=admin
OutputDir=..\..\dist
OutputBaseFilename=WandEnhancerSetup
SetupIconFile=..\assets\appicon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autopatch"; Description: "Keep Wand patched automatically after updates"; GroupDescription: "Auto-patch:"; Flags: checkedonce

[Files]
Source: "{#OutputDir}\WandEnhancer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#OutputDir}\AutoPatch\WandEnhancer.AutoPatch.exe"; DestDir: "{app}\AutoPatch"; Flags: ignoreversion
Source: "{#OutputDir}\AutoPatch\WandEnhancer.Core.dll"; DestDir: "{app}\AutoPatch"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--enable-autopatch ""{code:GetWandPath}"""; Description: "Enable auto-patch"; Flags: runascurrentuser waituntilterminated; Check: ShouldEnableAutoPatch

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--disable-autopatch"; Flags: runascurrentuser waituntilterminated; RunOnceId: DisableAutoPatch

[Code]
var
  WandPathPage: TInputDirWizardPage;
  DetectedWandPath: string;

function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) or
            RegQueryDWordValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  if Result then
    Result := Release >= 528372;
end;

function NormalizePath(const Path: string): string;
begin
  Result := Path;
  if Result = '' then Exit;
  // Remove trailing backslash unless this is a drive root like C:\
  while (Length(Result) > 1) and (Result[Length(Result)] = '\') do
    SetLength(Result, Length(Result) - 1);
end;

function LooksLikeWandPath(Path: string): Boolean;
begin
  // Keep this in sync with WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath.
  // resources may be a file or directory in some Wand builds.
  Result := (Path <> '') and
            DirExists(Path) and
            FileExists(Path + '\Wand.exe') and
            (DirExists(Path + '\resources') or FileExists(Path + '\resources')) and
            FileExists(Path + '\app.asar');
end;

// Parses a dotted version string (e.g. "12.43.1") into a comparable integer.
// Assumes each segment fits in a normal integer; -1 means invalid/unparseable.
function ParseVersionToInt(const Version: string): Int64;
var
  Segment: string;
  DotPos: Integer;
  Value, Multiplier: Int64;
  Remaining: string;
begin
  Result := 0;
  Remaining := Version;
  Multiplier := 100000000; // enough room for ~4 segments of 4 digits each

  while Remaining <> '' do
  begin
    DotPos := Pos('.', Remaining);
    if DotPos > 0 then
    begin
      Segment := Copy(Remaining, 1, DotPos - 1);
      Remaining := Copy(Remaining, DotPos + 1, Length(Remaining) - DotPos);
    end
    else
    begin
      Segment := Remaining;
      Remaining := '';
    end;

    Value := StrToInt64Def(Segment, -1);
    if Value < 0 then
    begin
      Result := -1;
      Exit;
    end;

    Result := Result + (Value * Multiplier);
    Multiplier := Multiplier div 10000;
    if Multiplier < 1 then Multiplier := 1;
  end;
end;

// Returns the latest app-* subfolder under BasePath, or '' if none exists.
// Does not validate the subfolder contents.
function FindLatestAppSubfolderName(BasePath: string): string;
var
  FindRec: TFindRec;
  BestName, VersionPart: string;
  BestVersion, CurrentVersion: Int64;
  SearchPath: string;
begin
  Result := '';
  if BasePath = '' then Exit;

  SearchPath := NormalizePath(BasePath) + '\app-*';
  BestName := '';
  BestVersion := -1;

  if FindFirst(SearchPath, FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          // FindRec.Name is like "app-12.43.1".
          VersionPart := Copy(FindRec.Name, 5, Length(FindRec.Name) - 4);
          CurrentVersion := ParseVersionToInt(VersionPart);

          if (BestName = '') or (CurrentVersion > BestVersion) then
          begin
            BestName := FindRec.Name;
            BestVersion := CurrentVersion;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;

  Result := BestName;
end;

// Resolves a user-selected or auto-detected folder to the actual folder that
// should be patched. Accepts:
//   - a direct Wand payload folder (has Wand.exe + resources + app.asar)
//   - a WeMod "root" folder with app-* versioned payload subfolders
//   - a specific app-* versioned payload subfolder
// Returns '' if no valid payload can be found.
function ResolveWandPath(BasePath: string): string;
var
  LatestName: string;
  NormalizedPath: string;
begin
  Result := '';
  if BasePath = '' then Exit;

  NormalizedPath := NormalizePath(BasePath);

  // Case 1: already a valid payload folder.
  if LooksLikeWandPath(NormalizedPath) then
  begin
    Result := NormalizedPath;
    Exit;
  end;

  // Case 2: an app-* subfolder was selected directly.
  if (Pos('\app-', NormalizedPath) > 0) and
     FileExists(NormalizedPath + '\Wand.exe') then
  begin
    Result := NormalizedPath;
    Exit;
  end;

  // Case 3: WeMod root with versioned app-* subfolders.
  LatestName := FindLatestAppSubfolderName(NormalizedPath);
  if LatestName <> '' then
  begin
    Result := NormalizedPath + '\' + LatestName;
    if not LooksLikeWandPath(Result) then
      Result := '';
  end;
end;

// Accepts either a direct Wand payload folder or a WeMod-style folder.
// WeMod keeps a launcher stub (Wand.exe) in the root and the real payload
// in app-* subfolders; accept the root as long as Wand.exe is present.
function IsWandRootFolder(Path: string): Boolean;
var
  NormalizedPath: string;
begin
  Result := False;
  if Path = '' then Exit;

  NormalizedPath := NormalizePath(Path);
  if not DirExists(NormalizedPath) then Exit;

  // Direct payload folder.
  if LooksLikeWandPath(NormalizedPath) then
  begin
    Result := True;
    Exit;
  end;

  // WeMod root with a Wand.exe launcher stub (we will resolve app-* later).
  if FileExists(NormalizedPath + '\Wand.exe') then
  begin
    Result := True;
    Exit;
  end;

  // Specific app-* subfolder selected directly.
  if (Pos('\app-', NormalizedPath) > 0) and FileExists(NormalizedPath + '\Wand.exe') then
  begin
    Result := True;
    Exit;
  end;
end;

function TryGetWandPathFromRegistry(RootKey: Integer): string;
var
  UninstallKey: string;
  SubKeyNames: TArrayOfString;
  I: Integer;
  DisplayName, InstallLocation: string;
begin
  Result := '';
  UninstallKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  if RegGetSubkeyNames(RootKey, UninstallKey, SubKeyNames) then
  begin
    for I := 0 to GetArrayLength(SubKeyNames) - 1 do
    begin
      if RegQueryStringValue(RootKey, UninstallKey + '\' + SubKeyNames[I], 'DisplayName', DisplayName) and
         ((Pos('Wand', DisplayName) > 0) or (Pos('WeMod', DisplayName) > 0)) and
         RegQueryStringValue(RootKey, UninstallKey + '\' + SubKeyNames[I], 'InstallLocation', InstallLocation) and
         (InstallLocation <> '') then
      begin
        if IsWandRootFolder(InstallLocation) then
        begin
          Result := InstallLocation;
          Exit;
        end;
      end;
    end;
  end;
end;

function GetWandPathAuto: string;
var
  Candidates: TArrayOfString;
  I: Integer;
begin
  Result := '';

  // Try registry first (per-user then per-machine).
  Result := TryGetWandPathFromRegistry(HKCU);
  if Result <> '' then Exit;

  Result := TryGetWandPathFromRegistry(HKLM);
  if Result <> '' then Exit;

  // Fallback to well-known locations.
  SetArrayLength(Candidates, 5);
  Candidates[0] := ExpandConstant('{localappdata}') + '\Programs\Wand';
  Candidates[1] := ExpandConstant('{localappdata}') + '\Wand';
  Candidates[2] := ExpandConstant('{localappdata}') + '\WeMod';
  Candidates[3] := ExpandConstant('{pf}') + '\Wand';
  Candidates[4] := ExpandConstant('{pf32}') + '\Wand';

  for I := 0 to GetArrayLength(Candidates) - 1 do
  begin
    if IsWandRootFolder(Candidates[I]) then
    begin
      Result := Candidates[I];
      Exit;
    end;
  end;

  Result := '';
end;

procedure InitializeWizard;
var
  WandPath: string;
  PageCaption: string;
begin
  WandPath := GetWandPathAuto;

  if WandPath = '' then
  begin
    PageCaption := 'Select Wand installation folder';
    WandPathPage := CreateInputDirPage(wpSelectDir,
      PageCaption,
      'Auto-patch needs to know where Wand is installed.',
      'Select the WeMod/Wand folder. If Wand is in a versioned app-* subfolder, select the parent folder.',
      False, '');
    WandPathPage.Add('');
    WandPathPage.Values[0] := ExpandConstant('{localappdata}') + '\WeMod';
    WandPathPage.Buttons[0].Caption := 'WeMod/Wand folder';
    WandPathPage.Edits[0].ReadOnly := False;
  end
  else
  begin
    DetectedWandPath := WandPath;
  end;
end;

function GetWandPath(Param: string): string;
begin
  // Return the root WeMod/Wand folder. The application resolves it to the
  // latest app-* payload subfolder internally.
  if DetectedWandPath <> '' then
    Result := DetectedWandPath
  else if (WandPathPage <> nil) and (WandPathPage.Values[0] <> '') then
    Result := WandPathPage.Values[0]
  else
    Result := '';
end;

function ShouldEnableAutoPatch: Boolean;
var
  Path: string;
begin
  Result := WizardIsTaskSelected('autopatch');
  if not Result then Exit;

  Path := GetWandPath('');
  if Path = '' then
  begin
    Result := False;
    Exit;
  end;

  if not IsWandRootFolder(Path) then
  begin
    MsgBox('The selected Wand folder is missing Wand.exe or a valid app-* payload subfolder. Auto-patch will not be enabled.', mbError, MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (WandPathPage <> nil) and (CurPageID = WandPathPage.ID) then
  begin
    if not IsWandRootFolder(WandPathPage.Values[0]) then
    begin
      MsgBox('Please select the WeMod/Wand folder that contains Wand.exe.' + #13#10 +
             'If the actual files are in a versioned subfolder (e.g. app-12.43.1), select the parent WeMod folder and the installer will pick the latest version.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not IsDotNet48Installed then
  begin
    MsgBox('.NET Framework 4.8 or later is required but was not detected.' + #13#10 +
           'Please install it from https://dotnet.microsoft.com/download/dotnet-framework/net48 then run this installer again.',
           mbCriticalError, MB_OK);
    Result := False;
  end;
end;
