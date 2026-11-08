; =====================================================================
; JGF Jewel Gold Finance — Inno Setup Script
; Supports two build modes: Server (PC1) and Client (PC2)
; =====================================================================

#ifndef APP_VERSION
  #define APP_VERSION "1.2.0"
#endif

#ifndef MODE
  #define MODE "Server"
#endif

#define MyAppName "Jeba Gold Finance"
#define MyAppPublisher "Jeba Gold Finance"
#define MyAppExeName "JGF.Desktop.exe"

[Setup]
AppId={{C2894A08-2A62-4C95-B330-8DF4275CE491}}
AppName={#MyAppName} ({#MODE})
AppVersion={#APP_VERSION}
AppVerName={#MyAppName} {#APP_VERSION} ({#MODE})
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\JGF
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\Releases
OutputBaseFilename=JGF-{#MODE}-Setup-{#APP_VERSION}
SetupIconFile=..\src\JGF.Desktop\Assets\app.ico
UninstallDisplayIcon={app}\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
; Durable ProgramData directories preserved across all updates and never uninstalled
Name: "{commonappdata}\JGF"; Flags: uninsneveruninstall; Permissions: users-modify authusers-modify
Name: "{commonappdata}\JGF\Logs"; Flags: uninsneveruninstall; Permissions: users-modify authusers-modify
Name: "{commonappdata}\JGF\Backups"; Flags: uninsneveruninstall; Permissions: users-modify authusers-modify
Name: "{commonappdata}\JGF\Data"; Flags: uninsneveruninstall; Permissions: users-modify authusers-modify

[Files]
; 1. Application Binaries (self-contained win-x64 Release)
Source: "..\src\JGF.Desktop\bin\Release\net8.0-windows\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 2. Tamil Unicode Font (Noto Sans Tamil) for Receipt and Report rendering
Source: "fonts\NotoSansTamil-Regular.ttf"; DestDir: "{autofonts}"; FontInstall: "Noto Sans Tamil"; Flags: onlyifdoesntexist uninsneveruninstall

; 3. Setup & Security Scripts bundled for PC1 Server mode
Source: "..\scripts\security\harden-postgresql.ps1"; DestDir: "{app}\scripts\security"; Flags: ignoreversion; Check: IsServerMode
Source: "..\scripts\security\configure-windows-firewall.ps1"; DestDir: "{app}\scripts\security"; Flags: ignoreversion; Check: IsServerMode
Source: "..\scripts\backup\run-automated-backup.ps1"; DestDir: "{app}\scripts\backup"; Flags: ignoreversion; Check: IsServerMode

; 4. Optional bundled PostgreSQL unattended installer (if present in packaging\prerequisites)
#if FileExists("prerequisites\postgresql-installer.exe")
Source: "prerequisites\postgresql-installer.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall nocompression; Check: HasBundledPgInstaller
#endif

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ConfigPage: TInputQueryWizardPage;
  PgPasswordEdit: Integer;
  Pc2IpEdit: Integer;
  AppPasswordEdit: Integer;
  Pc1IpEdit: Integer;

function IsServerMode: Boolean;
begin
  Result := (ExpandConstant('{#MODE}') = 'Server');
end;

function HasBundledPgInstaller: Boolean;
begin
  Result := FileExists(ExpandConstant('{src}\prerequisites\postgresql-installer.exe')) or
            FileExists(ExpandConstant('{#SourcePath}\prerequisites\postgresql-installer.exe'));
end;

procedure InitializeWizard;
begin
  if IsServerMode then
  begin
    ConfigPage := CreateInputQueryPage(wpSelectDir,
      'JGF Server (PC1) Configuration',
      'PostgreSQL Database & Network Security Settings',
      'Please enter the database administrative credentials and the LAN IP of PC2 (Counter 2 client).');

    PgPasswordEdit := ConfigPage.Add('PostgreSQL Master Password (postgres user):', True);
    Pc2IpEdit := ConfigPage.Add('PC2 Client LAN IP Address (Counter 2):', False);
    AppPasswordEdit := ConfigPage.Add('JGF Application Database Password (jgf_app):', True);

    ConfigPage.Values[PgPasswordEdit] := 'postgres';
    ConfigPage.Values[Pc2IpEdit] := '192.168.1.101';
    ConfigPage.Values[AppPasswordEdit] := 'JgfSecure_AppPassword2026!';
  end
  else
  begin
    ConfigPage := CreateInputQueryPage(wpSelectDir,
      'JGF Client (PC2) Connection Settings',
      'Connect to PC1 Database Server over LAN',
      'Please enter the LAN IP address of the PC1 Database Server.');

    Pc1IpEdit := ConfigPage.Add('PC1 Server LAN IP Address:', False);
    AppPasswordEdit := ConfigPage.Add('JGF Application Database Password (jgf_app):', True);

    ConfigPage.Values[Pc1IpEdit] := '192.168.1.100';
    ConfigPage.Values[AppPasswordEdit] := 'JgfSecure_AppPassword2026!';
  end;
end;

procedure WriteProductionAppSettings(Host, Port, Database, Username, Password: String);
var
  JsonContent: String;
  ConfigFilePath: String;
begin
  JsonContent := '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "DefaultConnection": "Host=' + Host + ';Port=' + Port + ';Database=' + Database + ';Username=' + Username + ';Password=' + Password + ';SSL Mode=Prefer;Timeout=5;Command Timeout=15;"' + #13#10 +
    '  },' + #13#10 +
    '  "Serilog": {' + #13#10 +
    '    "MinimumLevel": "Information"' + #13#10 +
    '  }' + #13#10 +
    '}';

  // 1. Write to durable ProgramData path
  ConfigFilePath := ExpandConstant('{commonappdata}\JGF\appsettings.json');
  SaveStringToFile(ConfigFilePath, JsonContent, False);

  // 2. Also write to App directory
  ConfigFilePath := ExpandConstant('{app}\appsettings.json');
  SaveStringToFile(ConfigFilePath, JsonContent, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PgSuperPass, Pc2Ip, AppPass, Pc1Ip: String;
  BundledInstallerPath: String;
  PowerShellCmd: String;
  HardenScript, FirewallScript, BackupScript: String;
begin
  if CurStep = ssPostInstall then
  begin
    if IsServerMode then
    begin
      PgSuperPass := ConfigPage.Values[PgPasswordEdit];
      Pc2Ip := ConfigPage.Values[Pc2IpEdit];
      AppPass := ConfigPage.Values[AppPasswordEdit];

      // 1. Silent install of PostgreSQL if bundled installer exists in tmp
      BundledInstallerPath := ExpandConstant('{tmp}\postgresql-installer.exe');
      if FileExists(BundledInstallerPath) then
      begin
        WizardForm.StatusLabel.Caption := 'Installing PostgreSQL Database Engine silently...';
        Exec(BundledInstallerPath,
          '--mode unattended --unattendedmodeui none --superpassword "' + PgSuperPass + '" --servicepassword "' + PgSuperPass + '" --serverport 5432',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      end;

      // 2. Run PostgreSQL hardening script
      HardenScript := ExpandConstant('{app}\scripts\security\harden-postgresql.ps1');
      if FileExists(HardenScript) then
      begin
        WizardForm.StatusLabel.Caption := 'Hardening PostgreSQL and provisioning jgf_app role...';
        PowerShellCmd := '-NoProfile -ExecutionPolicy Bypass -File "' + HardenScript + '" -Pc2LanIp "' + Pc2Ip + '" -AppPassword "' + AppPass + '"';
        Exec('powershell.exe', PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      end;

      // 3. Configure Windows Firewall
      FirewallScript := ExpandConstant('{app}\scripts\security\configure-windows-firewall.ps1');
      if FileExists(FirewallScript) then
      begin
        WizardForm.StatusLabel.Caption := 'Configuring Windows Firewall inbound rule for PC2...';
        PowerShellCmd := '-NoProfile -ExecutionPolicy Bypass -File "' + FirewallScript + '" -Pc2IpAddress "' + Pc2Ip + '"';
        Exec('powershell.exe', PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      end;

      // 4. Register Scheduled Backup Task in Windows Task Scheduler
      BackupScript := ExpandConstant('{app}\scripts\backup\run-automated-backup.ps1');
      if FileExists(BackupScript) then
      begin
        WizardForm.StatusLabel.Caption := 'Registering automated 9:00 PM database backup task...';
        PowerShellCmd := '-NoProfile -ExecutionPolicy Bypass -Command "' +
          'Unregister-ScheduledTask -TaskName ''JGF-Daily-Automated-Backup'' -Confirm:$false -ErrorAction SilentlyContinue; ' +
          '$action = New-ScheduledTaskAction -Execute ''PowerShell.exe'' -Argument ''-NoProfile -ExecutionPolicy Bypass -File \"' + BackupScript + '\"''; ' +
          '$trigger = New-ScheduledTaskTrigger -Daily -At ''21:00''; ' +
          '$principal = New-ScheduledTaskPrincipal -UserId ''SYSTEM'' -LogonType ServiceAccount -RunLevel Highest; ' +
          'Register-ScheduledTask -TaskName ''JGF-Daily-Automated-Backup'' -Action $action -Trigger $trigger -Principal $principal -Description ''Daily automated PostgreSQL backup for JGF Jewel Gold Finance'' | Out-Null"';
        Exec('powershell.exe', PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      end;

      // 5. Write Server production appsettings.json
      WriteProductionAppSettings('localhost', '5432', 'jgf_db', 'jgf_app', AppPass);
    end
    else
    begin
      // Client Mode
      Pc1Ip := ConfigPage.Values[Pc1IpEdit];
      AppPass := ConfigPage.Values[AppPasswordEdit];

      // Write Client production appsettings.json pointing to PC1 Server LAN IP
      WriteProductionAppSettings(Pc1Ip, '5432', 'jgf_db', 'jgf_app', AppPass);
    end;
  end;
end;
