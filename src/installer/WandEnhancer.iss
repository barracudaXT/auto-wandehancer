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

function LooksLikeWandPath(Path: string): Boolean;
begin
  // Keep this in sync with WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath.
  Result := (Path <> '') and
            DirExists(Path) and
            FileExists(Path + '\Wand.exe') and
            DirExists(Path + '\resources') and
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

// Wand/WeMod sometimes installs under a versioned "app-x.y.z" subfolder
// (e.g. %LocalAppData%\WeMod\app-12.43.1). This resolves the parent path
// to the actual application folder if possible.
function ResolveWandPath(BasePath: string): string;
var
  FindRec: TFindRec;
  BestPath, Candidate, VersionPart: string;
  BestVersion, CurrentVersion: Int64;
begin
  Result := '';
  if BasePath = '' then Exit;

  if LooksLikeWandPath(BasePath) then
  begin
    Result := BasePath;
    Exit;
  end;

  // Look for app-* subfolders and prefer the numerically highest version.
  BestPath := '';
  BestVersion := -1;
  if FindFirst(BasePath + '\app-*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Candidate := BasePath + '\' + FindRec.Name;
          if LooksLikeWandPath(Candidate) then
          begin
            // FindRec.Name is like "app-12.43.1".
            VersionPart := Copy(FindRec.Name, 5, Length(FindRec.Name) - 4);
            CurrentVersion := ParseVersionToInt(VersionPart);

            if (BestPath = '') or (CurrentVersion > BestVersion) then
            begin
              BestPath := Candidate;
              BestVersion := CurrentVersion;
            end;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;

  Result := BestPath;
end;

function TryGetWandPathFromRegistry(RootKey: Integer): string;
var
  UninstallKey: string;
  SubKeyNames: TArrayOfString;
  I: Integer;
  DisplayName, InstallLocation, ResolvedPath: string;
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
        ResolvedPath := ResolveWandPath(InstallLocation);
        if ResolvedPath <> '' then
        begin
          Result := ResolvedPath;
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
  Resolved: string;
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
    Resolved := ResolveWandPath(Candidates[I]);
    if Resolved <> '' then
    begin
      Result := Resolved;
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
  end
  else
  begin
    DetectedWandPath := WandPath;
  end;
end;

function GetWandPath(Param: string): string;
var
  SelectedPath, ResolvedPath: string;
begin
  if DetectedWandPath <> '' then
    Result := DetectedWandPath
  else if (WandPathPage <> nil) and (WandPathPage.Values[0] <> '') then
  begin
    SelectedPath := WandPathPage.Values[0];
    ResolvedPath := ResolveWandPath(SelectedPath);
    if ResolvedPath <> '' then
      Result := ResolvedPath
    else
      Result := SelectedPath;
  end
  else
    Result := '';
end;

function ShouldEnableAutoPatch: Boolean;
var
  Path, ResolvedPath: string;
begin
  Result := WizardIsTaskSelected('autopatch');
  if not Result then Exit;

  Path := GetWandPath('');
  ResolvedPath := ResolveWandPath(Path);
  if ResolvedPath <> '' then
    Path := ResolvedPath;

  if Path = '' then
  begin
    Result := False;
    Exit;
  end;

  if not LooksLikeWandPath(Path) then
  begin
    MsgBox('The selected Wand folder is missing Wand.exe, resources or app.asar. Auto-patch will not be enabled.', mbError, MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (WandPathPage <> nil) and (CurPageID = WandPathPage.ID) then
  begin
    if ResolveWandPath(WandPathPage.Values[0]) = '' then
    begin
      MsgBox('Please select a valid Wand installation folder containing Wand.exe, resources and app.asar.' + #13#10 +
             'If Wand is installed under a versioned subfolder (e.g. app-12.43.1), select the parent WeMod folder.', mbError, MB_OK);
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
