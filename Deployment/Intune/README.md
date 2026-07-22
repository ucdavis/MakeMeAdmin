# Intune Win32 deployment

Place the release MSI beside these scripts and name it
`MakeMeAdmin-2.4.2-en-us-x64.msi`. Use Microsoft Win32 Content Prep Tool with a
separate output directory so generated `.intunewin` files are never included in
the source folder.

Intune commands:

```text
Install:   cmd.exe /d /c Install.cmd
Uninstall: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall-MakeMeAdmin.ps1
```

Use `Detect-MakeMeAdmin.ps1` as a custom detection rule and install in the System
context. The wrapper configures the organization-specific values documented in
`WINDOWS-HELLO-AUTHENTICATION.md`, including `Authentication Mode = 3`.

Logs are written under:

```text
C:\ProgramData\Microsoft\IntuneManagementExtension\Logs
```
