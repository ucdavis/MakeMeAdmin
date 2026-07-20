# Windows Hello release validation

Validation date: 2026-07-20

This record covers the Phase 5 and Phase 6 validation performed for the Windows
Hello reauthentication extension. It distinguishes automated evidence from tests
that must run on a disposable or physically representative endpoint.

## Completed automated validation

- The ADMX and English ADML parse as XML, and every local string and presentation
  reference resolves.
- Debug x64 and Release x64 builds succeeded for the service, local UI, remote UI,
  regression harness, and Danish, German, English, and French MSI packages.
- Unrestricted Windows Installer ICE validation completed for all eight localized
  Debug and Release packages with no validation errors. Each locale reports the
  existing ICE69 warning for the optional remote shortcut referencing a file in a
  different component within the same feature. ICE57 remains the package's
  intentional suppression.
- All 43 focused regression cases passed in both Debug and Release (86 executions):
  six service authorization, ten password/secret-cleanup, eight native Windows
  Hello result mappings, and nineteen settings/coordinator decisions.
- MSI decompilation confirmed that `MakeMeAdminUI.exe` and
  `MakeMeAdminService.exe` are packaged. No Windows SDK Contracts or
  `System.Runtime.WindowsRuntime` assembly is packaged; the contracts reference is
  compile-time metadata only.
- The package retains product version 2.4.1 and UpgradeCode
  `{9C690479-3987-48A8-8BDA-118EF1F93CDD}`.

## Security failure-path review

| Condition | Observed decision before grant request |
| --- | --- |
| Unsupported configured mode | Throws and is caught by the UI; no service call |
| Hello verified | May continue only after the reason requirement is satisfied |
| Hello canceled | Stops; no automatic password fallback |
| Hello unavailable in mode 2 | Stops and displays unavailable status |
| Hello unavailable in mode 3 | Invokes current-user password path |
| Retries exhausted or unknown Hello result | Fails closed; no password fallback |
| Hello or password verifier exception | UI catch displays failure; no service call |
| Password canceled, invalid, or different SID | Stops before service call |
| Required reason canceled or invalid | Stops before service call |
| Local service caller absent, denied, or not allowed | Service authorization rejects the request |

The grant worker starts only when authentication succeeded and the reason gate is
satisfied. The service separately evaluates the named-pipe caller's Windows
identity against local and, when applicable, remote allow/deny rules.

The remaining trust limitation is deliberate and documented: the Windows Hello
result is not attested to the LocalSystem service. The service authorizes the
caller but cannot prove that the caller completed the UI's Hello prompt.

## Completed physical review

The Phase 4 review endpoint passed the normal Windows Hello PIN path, cancellation
path, and the mode 3 password choice. That confirms the principal UI integration
on the tested Windows 11 endpoint, but does not replace the broader device matrix
below.

## Required disposable-VM and physical-device validation

The following tests are not safe or representative on the development host and
remain release gates:

- Clean install, service automatic start, repair, and uninstall on a disposable
  Windows 11 VM.
- Upgrade using the organization's exact previous MSI and deployment command.
  Because both packages may report version 2.4.1, confirm ProductCode and
  same-version upgrade behavior explicitly.
- End-to-end named-pipe denial using an unauthorized local account, plus successful
  grant, configured expiration, removal on logout, and service-stop cleanup.
- Entra joined, hybrid joined, domain, and local-account coverage required by the
  organization.
- Production Windows Hello for Business policies with PIN, face/fingerprint,
  explicit password choice, Hello unavailable, and cancellation.
- RDP, fast user switching, and concurrent-session behavior.
- Endpoint-management install/detection, code signing, and rollback procedures.

Use a snapshot before each lifecycle sequence and collect verbose MSI logs, Make
Me Admin event logs, service state, local Administrators membership, registry
policy state, and timestamps for the grant and removal events.

## WiX 3 custom-action project decision

`WiXCustomAction` is still listed in the solution and requires the older WiX 3
toolset, but it is not referenced by the WiX 6 installer. Building the whole
solution therefore fails on systems without WiX 3 even though the actual package
build succeeds. Removal or migration should be a separate cleanup commit so it
does not obscure the authentication and installer validation history.
