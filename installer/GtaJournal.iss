; Установщик GTA Journal (Inno Setup 6).
; Собирается скриптом installer\build.ps1, который передаёт AppVersion, Arch и SourceDir.
; Ставится в профиль пользователя без прав администратора — поэтому автообновление проходит без UAC.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-" + Arch
#endif

#define AppName "GTA Journal"
#define AppExe "GtaJournal.exe"
#define AppUrl "https://github.com/HM-Province/GtaJournal-Reloaded"
; Должен совпадать с Program.AppMutexName
#define AppMutex "GtaJournal.AppMutex"

[Setup]
AppId={{9AB390A4-2F25-4399-B5CD-27115D274882}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=HM-Province
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\GtaJournal
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableReadyPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.17763
#if Arch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#endif
SetupIconFile=..\GtaJournal\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=GtaJournal-Setup-{#AppVersion}-{#Arch}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Закрытие приложения проверяем сами по мьютексу (см. [Code]).
CloseApplications=no
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; Обычная установка — галочка «Запустить» на последней странице.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Автообновление (тихий режим) — запускаем новую версию сразу.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: WizardSilent

[Code]
const
  RunningMessage = 'GTA Journal сейчас запущен.' + #13#10 +
    'Закройте его (в том числе из трея: правый клик по значку → «Выход») и нажмите «OK».';

{ Ждёт закрытия приложения. В тихом режиме (автообновление) приложение закрывается само сразу после
  запуска установщика — ждём до 30 секунд; в обычном режиме просим пользователя закрыть его. }
function WaitForAppClosed(Silent: Boolean): Boolean;
var
  Attempts: Integer;
begin
  Attempts := 0;
  while CheckForMutexes('{#AppMutex}') do
  begin
    if Silent then
    begin
      if Attempts >= 60 then
      begin
        Result := False;
        Exit;
      end;
      Attempts := Attempts + 1;
      Sleep(500);
    end
    else if MsgBox(RunningMessage, mbError, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;

function InitializeSetup(): Boolean;
begin
  Result := WaitForAppClosed(WizardSilent());
end;

function InitializeUninstall(): Boolean;
begin
  Result := WaitForAppClosed(UninstallSilent());
end;
