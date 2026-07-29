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
         (InstallLocation <> '') and
         FileExists(InstallLocation + '\Wand.exe') then
      begin
        Result := InstallLocation;
        Exit;
      end;
    end;
  end;
end;

function LooksLikeWandPath(Path: string): Boolean;
begin
  Result := (Path <> '') and DirExists(Path) and FileExists(Path + '\Wand.exe');
end;

function GetWandPathAuto: string;
var
  Candidates: TArrayOfString;
  I: Integer;
begin
  Result := '';

  // Try registry first (per-user then per-machine).
  Result := TryGetWandPathFromRegistry(HKCU);
  if LooksLikeWandPath(Result) then Exit;

  Result := TryGetWandPathFromRegistry(HKLM);
  if LooksLikeWandPath(Result) then Exit;

  // Fallback to well-known locations.
  SetArrayLength(Candidates, 4);
  Candidates[0] := ExpandConstant('{localappdata}') + '\Programs\Wand';
  Candidates[1] := ExpandConstant('{localappdata}') + '\Wand';
  Candidates[2] := ExpandConstant('{pf}') + '\Wand';
  Candidates[3] := ExpandConstant('{pf32}') + '\Wand';

  for I := 0 to GetArrayLength(Candidates) - 1 do
  begin
    if LooksLikeWandPath(Candidates[I]) then
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
      'Select the folder that contains Wand.exe, then click Next.',
      False, '');
    WandPathPage.Add('');
    WandPathPage.Values[0] := ExpandConstant('{pf32}') + '\Wand';
  end
  else
  begin
    DetectedWandPath := WandPath;
  end;
end;

function GetWandPath(Param: string): string;
begin
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

  if not DirExists(Path) or not FileExists(Path + '\Wand.exe') then
  begin
    MsgBox('The selected Wand folder does not contain Wand.exe. Auto-patch will not be enabled.', mbError, MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (WandPathPage <> nil) and (CurPageID = WandPathPage.ID) then
  begin
    if not LooksLikeWandPath(WandPathPage.Values[0]) then
    begin
      MsgBox('Please select a valid Wand installation folder containing Wand.exe.', mbError, MB_OK);
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
