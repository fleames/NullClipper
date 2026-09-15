#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif

#define MyAppName "NullClipper"
#define MyAppPublisher "fleames"
#define MyAppURL "https://github.com/fleames/NullClipper"
#define MyAppExeName "NullClipper.exe"

[Setup]
AppId={{8F3C2A91-6B47-4E0D-9C12-A7D4E5B60821}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases/latest
DefaultDirName={localappdata}\Programs\NullClipper
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=NullClipper-Setup
SetupIconFile=..\Assets\clipper.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Snip or record a GIF"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch NullClipper"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
var
  DotnetExe: String;
begin
  Result := True;
  DotnetExe := ExpandConstant('{commonpf64}\dotnet\dotnet.exe');
  if not FileExists(DotnetExe) then
    DotnetExe := ExpandConstant('{localappdata}\Microsoft\dotnet\dotnet.exe');
  if not FileExists(DotnetExe) then
    MsgBox(
      'NullClipper needs the .NET 8 Desktop Runtime (x64).' + #13#10#13#10 +
      'Setup will continue. If the app does not start, install the runtime from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/8.0',
      mbInformation, MB_OK);
end;
