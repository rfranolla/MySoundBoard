; MySoundBoard Installer
; Inno Setup 6.x
;
; Build from the repo root (SoundBoard\):
;   dotnet publish MySoundBoard\MySoundBoard.csproj /p:PublishProfile=Release-x64 -c Release
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MySoundBoard.iss

#define MyAppName      "MySoundBoard"
#define MyAppPublisher "Riley Franolla"
#define MyAppExeName   "MySoundBoard.exe"
#define MyAppVersion   GetVersionNumbersString("..\publish\MySoundBoard.exe")

[Setup]
AppId={{A3F7C2E1-88B4-4D5F-9C3A-1E6D0F2B4A78}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=MySoundBoard-Setup-{#MyAppVersion}
SetupIconFile=..\MySoundBoard\SoundboardIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; All published app files (includes runtimes\ subfolder automatically)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; App icon bundled separately for uninstall display entry
Source: "..\MySoundBoard\SoundboardIcon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";             Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\SoundboardIcon.ico"
Name: "{group}\Uninstall {#MyAppName}";   Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\SoundboardIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check for .NET 8 Desktop Runtime by looking for the System.Windows.Forms.dll file
// in the shared framework directory. This is more reliable than registry checks.
function IsDotNetDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
  BasePath: String;
begin
  Result := False;
  BasePath := ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App\');
  
  // Search for any 8.x.x version directory
  if FindFirst(BasePath + '8.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and $10 <> 0) then  // Check if it's a directory
      begin
        if FileExists(BasePath + FindRec.Name + '\System.Windows.Forms.dll') then
        begin
          Result := True;
          Break;
        end;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

// Before the wizard starts, offer to download and install the runtime if missing.
// TDownloadWizardPage is built into Inno Setup 6.1+ — no plugins required.
function InitializeSetup: Boolean;
var
  ResultCode: Integer;
  DownloadPage: TDownloadWizardPage;
begin
  Result := True;

  if not IsDotNetDesktopRuntimeInstalled then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime is required but was not found on this machine.' + #13#10 +
      'Click OK to download and install it automatically (requires internet access), or Cancel to abort.',
      mbConfirmation, MB_OKCANCEL) = IDOK then
    begin
      DownloadPage := CreateDownloadPage(
        SetupMessage(msgWizardPreparing),
        SetupMessage(msgPreparingDesc),
        nil);
      DownloadPage.Clear;
      DownloadPage.Add(
        'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe',
        'windowsdesktop-runtime-win-x64.exe',
        '');
      DownloadPage.Show;
      try
        DownloadPage.Download;
        Exec(ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'),
             '/install /quiet /norestart',
             '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      finally
        DownloadPage.Hide;
      end;
    end
    else
      Result := False;
  end;
end;