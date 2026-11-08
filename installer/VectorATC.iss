; Установщик Vector ATC (Inno Setup). Версию задаёт сборка: iscc /DAppVersion=0.1.5 installer\VectorATC.iss
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{6F0C4D1E-8A52-4B7B-9C3E-2D5A7B1E40AF}
AppName=Vector ATC
AppVersion={#AppVersion}
DefaultDirName={autopf}\Vector ATC
DefaultGroupName=Vector ATC
UninstallDisplayIcon={app}\VectorATC.exe
SetupIconFile=..\src\VectorAtc.App\Assets\app.ico
OutputDir=..\dist
OutputBaseFilename=VectorATC-Setup
Compression=lzma2
SolidCompression=yes
; Без прав администратора: ставится в профиль пользователя, окно запроса прав не нужно.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\src\VectorAtc.App\bin\publish-installer\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Vector ATC"; Filename: "{app}\VectorATC.exe"
Name: "{autodesktop}\Vector ATC"; Filename: "{app}\VectorATC.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VectorATC.exe"; Description: "{cm:LaunchProgram,Vector ATC}"; Flags: nowait postinstall skipifsilent
