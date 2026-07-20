# Windows Hello reauthentication

This extension can require an authorized local user to confirm their identity
before Make Me Admin asks the service to grant temporary administrator rights.
Windows Hello is the default experience in the recommended mode, while the user
can always choose a password instead.

## Requirements

- Make Me Admin and the .NET Framework 4.8 runtime.
- Windows 11 build 22000 or later for the window-owned Windows Hello prompt.
- A Windows Hello PIN, face, or fingerprint configured for the signed-in user
  when a Windows Hello mode is selected.

The project pins `Microsoft.Windows.SDK.Contracts` version `10.0.22621.3233` as
compile-time metadata for the desktop WinRT declarations. Windows supplies the
WinRT runtime implementation. The NuGet package is not deployed as an additional
application DLL by the MSI.

## Authentication modes

`Authentication Mode` is a registry DWORD with these supported values:

| Value | Mode | Behavior |
| ---: | --- | --- |
| 0 | None | No UI reauthentication. Existing authorization rules still apply. |
| 1 | Password | Shows a password-only Windows credential prompt. The authenticated token SID must match the signed-in user. |
| 2 | Windows Hello | Requires Windows Hello. No password fallback is offered. |
| 3 | Windows Hello with password fallback | Starts with Windows Hello and always shows **Use password instead**. Password is offered automatically only if Hello is unavailable. |

Mode 3 is the recommended setting when both Windows Hello and password must be
available. Canceling the Windows Hello prompt cancels the request; it does not
silently switch to password. Verification failure, excessive retries, unknown
native results, and exceptions all stop the request before the service call.

## Registry configuration and precedence

Machine policy:

```text
HKLM\SOFTWARE\Policies\Sinclair Community College\Make Me Admin
  Authentication Mode    REG_DWORD
```

Machine preference:

```text
HKLM\SOFTWARE\Sinclair Community College\Make Me Admin
  Authentication Mode    REG_DWORD
```

Settings are resolved in this order:

1. Policy `Authentication Mode`
2. Preference `Authentication Mode`
3. Policy `Require Authentication For Privileges` (legacy)
4. Preference `Require Authentication For Privileges` (legacy)
5. Default mode `None`

The legacy value maps enabled/nonzero to `Password` and disabled/zero to `None`.
An explicitly configured `Authentication Mode` outside 0-3 is not ignored; the
request fails closed.

For example, to configure the recommended preference locally from an elevated
PowerShell session:

```powershell
$path = 'HKLM:\SOFTWARE\Sinclair Community College\Make Me Admin'
New-Item -Path $path -Force | Out-Null
New-ItemProperty -Path $path -Name 'Authentication Mode' -Value 3 -PropertyType DWord -Force | Out-Null
```

For centrally enforced policy, use the policy path instead:

```powershell
$path = 'HKLM:\SOFTWARE\Policies\Sinclair Community College\Make Me Admin'
New-Item -Path $path -Force | Out-Null
New-ItemProperty -Path $path -Name 'Authentication Mode' -Value 3 -PropertyType DWord -Force | Out-Null
```

## Group Policy templates

Copy `Setup\GroupPolicy\SinclairMakeMeAdmin.admx` into the domain Central Store
`PolicyDefinitions` folder and copy
`Setup\GroupPolicy\en-US\SinclairMakeMeAdmin.adml` into its `en-US` subfolder.
The machine policy appears under **Sinclair Community College > Make Me Admin >
Authentication Mode**.

The template retains **Require Authentication to Obtain Privileges** for older
clients. On this client, the new dropdown takes precedence. The new policy text
also records that window-owned Windows Hello requires Windows 11 build 22000 or
later. Only the English (United States) language resource is supplied.

## Result and fallback behavior

- `Verified`: continue to the reason check and, if satisfied, request the grant.
- `Canceled`: stop. Never fall back automatically.
- Device not present, not configured, disabled by policy, or otherwise
  unavailable: mode 2 stops; mode 3 offers password.
- Retries, verification failure, an unknown native value, or an exception: stop.
- Selecting **Use password instead** in mode 3 invokes the password path directly.

The password prompt uses the current Windows username as a read-only identity and
accepts only a token whose SID equals the interactive user's SID. This prevents a
different administrator account from authorizing a request for the current user.

## Security boundary

Windows Hello verification occurs in the interactive UI process. It provides a
window-owned presence check and improves the normal user experience, but the
result is not cryptographically attested to the LocalSystem service. The service
independently enforces the existing allowed/denied entity policy and named-pipe
caller identity. A process already running as the same authorized user may still
be able to invoke the local service interface without reproducing the UI check.

Consequently, this feature must not be represented as a service-enforced strong
authentication boundary. Deploy it as UI reauthentication layered on top of the
service's authorization rules. A future stronger design would require a
single-use, service-verifiable proof bound to the caller, operation, and expiry.

## Packaging and servicing

The MSI remains a per-machine package with the existing UpgradeCode. Normal MSI
repair restores packaged files and service configuration; registry policy values
are administrator-managed and are not authored or removed by this feature.
Uninstall removes the application but does not delete organization policy.

Before broad deployment, validate install, repair, upgrade, and uninstall on a
disposable Windows 11 VM. Because this development build retains product version
2.4.1, test the organization's exact upgrade command and detection rules; Windows
Installer may treat two packages carrying the same product version as the same or
an ineligible major upgrade depending on their ProductCode.

`WiXCustomAction` is retained for now. It is present in the solution but is not
referenced by the WiX v4 package, so removing it is a separate cleanup change and
not part of the authentication feature.

## Deployment test checklist

- Test all four modes with an authorized standard user.
- Verify policy overrides preference and legacy values.
- Verify invalid mode values stop the request.
- In mode 3, verify Hello success, **Use password instead**, Hello unavailable,
  Hello cancellation, an incorrect password, and a different account password.
- Verify an unauthorized caller cannot add itself through the local service.
- Verify rights expire and are removed at the configured timeout and at logout
  when that policy is enabled.
- Test Entra joined, hybrid joined, and local/domain account devices as applicable.
- Test RDP, fast user switching, face/fingerprint, PIN, and password behavior on
  physical devices governed by the production Windows Hello for Business policy.
- Run unrestricted MSI ICE validation and inspect the signed deployment artifact.
