; Compile only with a locally installed Inno Setup compiler. The release script supplies
; AppVersion, SourceDir, and OutputDir after it validates the self-contained payload.
#ifndef AppVersion
  #error AppVersion must be supplied by scripts\Publish-WindowsRelease.ps1.
#endif
#ifndef SourceDir
  #error SourceDir must be supplied by scripts\Publish-WindowsRelease.ps1.
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by scripts\Publish-WindowsRelease.ps1.
#endif

[Setup]
AppId={{8DFF6D6D-6678-4455-9B24-CEB32A1D854A}
AppName=PasswordTool
AppVersion={#AppVersion}
AppPublisher=REPLACE_WITH_PUBLISHER
DefaultDirName={localappdata}\Programs\PasswordTool
DefaultGroupName=PasswordTool
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=PasswordTool-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayName=PasswordTool
UsePreviousAppDir=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PasswordTool"; Filename: "{app}\PasswordTool.WinForms.exe"

[Run]
Filename: "{app}\PasswordTool.WinForms.exe"; Description: "Launch PasswordTool"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Application binaries are removed by the uninstaller. Vault data intentionally lives in
; %LocalAppData%\PasswordTool, outside {app}, and is never removed by this installer.
