; =========================
;  RemoteSentinel - Inno Setup (CI-friendly)
; =========================

#define MyAppName    "RemoteSentinel"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.1"
#endif
#define MyPublisher  "MJ Devs"
#define MyExeName    "RemoteSentinel.exe"

; PublishDir por defecto: carpeta clásica de publish de VS relative a installer\
#ifndef PublishDir
  #define PublishDir  "..\bin\Release\net8.0-windows\publish"
#endif

; GUID CORREGIDO (antes tenía una { extra)
#define MyAppId      "{{65DA359F-BCFC-4763-BBBF-6A34E28930E6}"

; Branding: carpeta junto al setup.iss => installer\branding
#define BrandingDir  SourcePath + "branding"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVerName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename={#MyAppName}-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyExeName}
CloseApplications=yes
RestartApplications=yes
SetupIconFile={#BrandingDir}\rs.ico
WizardSmallImageBackColor=clWhite

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
; Publicado de tu app (asegúrate de haber hecho publish)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

; Recursos de interfaz (BMP, no PNG)
Source: "{#BrandingDir}\top-banner.bmp"; DestDir: "{tmp}"; Flags: dontcopy

[Tasks]
Name: "autorun"; Description: "Iniciar {#MyAppName} automáticamente al iniciar Windows"; \
    GroupDescription: "Opciones:"; Flags: checkedonce

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyExeName}"" --autorun"; Tasks: autorun

[Run]
Filename: "{app}\{#MyExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

; =========================
;  Código (UI extra)
; =========================
[Code]

var
  TopImg: TBitmapImage;
  WelcomeTitle, WelcomeSub: TNewStaticText;

procedure CreateTopImageAndWelcome;
var
  PageW, PageH: Integer;
  NewW, NewH: Integer;
  Ratio: Double;
begin
  ExtractTemporaryFile('top-banner.bmp');

  PageW := WizardForm.SelectTasksPage.ClientWidth;
  PageH := WizardForm.SelectTasksPage.ClientHeight;

  TopImg := TBitmapImage.Create(WizardForm.SelectTasksPage);
  TopImg.Parent := WizardForm.SelectTasksPage;
  TopImg.AutoSize := False;
  TopImg.Stretch  := True;
  TopImg.Bitmap.LoadFromFile(ExpandConstant('{tmp}\top-banner.bmp'));

  Ratio := TopImg.Bitmap.Height / TopImg.Bitmap.Width;
  NewW := ScaleX(100);
  NewH := Round(NewW * Ratio);
  TopImg.SetBounds((PageW - NewW) div 2, ScaleY(0), NewW, NewH);

  WelcomeTitle := TNewStaticText.Create(WizardForm.SelectTasksPage);
  WelcomeTitle.Parent := WizardForm.SelectTasksPage;
  WelcomeTitle.AutoSize := True;
  WelcomeTitle.Font.Style := [fsBold];
  WelcomeTitle.Caption := 'Bienvenido a ' + '{#MyAppName}';
  WelcomeTitle.Top := TopImg.Top + TopImg.Height + ScaleY(8);
  WelcomeTitle.Left := (PageW - WelcomeTitle.Width) div 2;

  WelcomeSub := TNewStaticText.Create(WizardForm.SelectTasksPage);
  WelcomeSub.Parent := WizardForm.SelectTasksPage;
  WelcomeSub.AutoSize := True;
  WelcomeSub.Caption := 'Configura a continuación cómo quieres que funcione la aplicación.';
  WelcomeSub.Top := WelcomeTitle.Top + WelcomeTitle.Height + ScaleY(4);
  WelcomeSub.Left := (PageW - WelcomeSub.Width) div 2;

  WizardForm.SelectTasksLabel.Hide;
  WizardForm.TasksList.Top := WelcomeSub.Top + WelcomeSub.Height + ScaleY(12);
  WizardForm.TasksList.Height := PageH - WizardForm.TasksList.Top - ScaleY(8);
end;

procedure InitializeWizard;
begin
  WizardForm.Caption := 'Instalar - ' + '{#MyAppName}';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then
    if TopImg = nil then
      CreateTopImageAndWelcome;
end;

procedure DeinitializeSetup;
begin
  if Assigned(WelcomeSub) then WelcomeSub.Free;
  if Assigned(WelcomeTitle) then WelcomeTitle.Free;
  if Assigned(TopImg) then TopImg.Free;
end;
