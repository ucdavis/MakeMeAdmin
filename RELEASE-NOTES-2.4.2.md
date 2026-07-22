# Make Me Admin 2.4.2

This customized release adds optional Windows Hello reauthentication to local
administrator-rights requests and uses a higher product version so it can upgrade
the upstream 2.4.1 package.

## Highlights

- Windows Hello, password, no-reauthentication, and Hello-with-password-fallback
  modes.
- Windows Hello is window-owned and mode 3 always offers **Use password instead**.
- Canceled, failed, unknown, and invalid authentication outcomes stop before the
  UI contacts the service.
- Password authentication accepts only credentials whose token SID matches the
  signed-in user.
- The privileged service independently enforces local and remote allow/deny rules.
- Intune Win32 and HTTPS PowerShell deployment examples are included in source.

## Security boundary

Windows Hello verification occurs in the interactive UI process and is not
cryptographically attested to the LocalSystem service. The service authorizes the
named-pipe caller but cannot independently prove that the caller completed the UI
prompt. Deploy this as UI reauthentication layered over service authorization,
not as a service-enforced strong-authentication boundary.

## Validation

- All 43 focused cases passed in Debug and Release (86 executions).
- Clean Debug and Release x64 builds produced Danish, German, English, and French
  MSI packages.
- Unrestricted Release ICE validation completed without errors. The existing
  ICE69 warning for the optional remote shortcut remains; ICE57 remains suppressed.
- Physical Windows Hello, cancellation, and explicit-password testing passed in
  Phase 4. Repeat upgrade and lifecycle testing with this 2.4.2 artifact before
  production assignment.

## English x64 MSI

- File: `MakeMeAdmin-2.4.2-en-us-x64.msi`
- Bytes: `2,543,616`
- SHA-256: `837577123F0611930977C1FEABC1A6BDB5764238C027B3B901B82239F63BD51D`
- ProductCode: `{F1870D1C-6440-4695-91B4-964C396E9FFE}`
- UpgradeCode: `{9C690479-3987-48A8-8BDA-118EF1F93CDD}`

The review artifact is unsigned. Sign the final deployment artifact before broad
production release.
