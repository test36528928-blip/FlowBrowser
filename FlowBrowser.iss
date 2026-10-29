#define MyAppName "FlowBrowser"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "FlowBrowser"
#define MyAppExeName "FlowBrowser.exe"

[Setup]
AppId={{A97C4C62-5F88-4A37-9B8A-4C1F6D1E82A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\FlowBrowser
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=Assets\FlowBrowser.ico
OutputDir=installer-output
OutputBaseFilename=FlowBrowser-Setup-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Ek kısayollar:"; Flags: unchecked

[Files]
Source: "publish\FlowBrowser.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Assets\FlowBrowser.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\FlowBrowser"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\FlowBrowser"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FlowBrowser"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "FlowBrowser'ı başlat"; Flags: postinstall nowait skipifsilent
