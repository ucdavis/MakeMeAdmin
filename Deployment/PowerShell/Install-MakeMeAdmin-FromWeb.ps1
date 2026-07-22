#Requires -Version 5.1

<#
.SYNOPSIS
Downloads and installs the customized Make Me Admin 2.4.2 MSI, then applies and
verifies the organization-specific registry configuration.
#>

[CmdletBinding()]
param(
    [ValidatePattern('^https://')]
    [string]$InstallerUrl = 'https://REPLACE-WITH-YOUR-CUSTOM-MSI-URL',
    [string]$InstallerPath = 'C:\Temp\MakeMeAdmin-2.4.2-en-us-x64.msi',
    [string]$ExpectedSha256 = '837577123F0611930977C1FEABC1A6BDB5764238C027B3B901B82239F63BD51D'
)

$ErrorActionPreference = 'Stop'
$logPath = 'C:\Temp\MakeMeAdmin-install.log'
try {
    if ($InstallerUrl -match 'REPLACE-WITH') {
        throw 'Set InstallerUrl to the published 2.4.2 release MSI URL.'
    }

    $installerDirectory = Split-Path -Parent $InstallerPath
    if (-not (Test-Path -LiteralPath $installerDirectory)) {
        New-Item -Path $installerDirectory -ItemType Directory -Force | Out-Null
    }

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $InstallerUrl -OutFile $InstallerPath -UseBasicParsing
    $actualHash = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash
    if ($actualHash -ne $ExpectedSha256) {
        throw "Installer SHA-256 verification failed. Expected '$ExpectedSha256'; received '$actualHash'."
    }

    $arguments = @('/i', ('"{0}"' -f $InstallerPath), '/qn', '/norestart', '/L*v', ('"{0}"' -f $logPath))
    $process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -notin 0, 1641, 3010) {
        throw "Windows Installer returned $($process.ExitCode). See '$logPath'."
    }

    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $settingsKey = $baseKey.CreateSubKey('SOFTWARE\Sinclair Community College\Make Me Admin')
        try {
            $settingsKey.SetValue('Allowed Entities', [string[]]@('Users'), [Microsoft.Win32.RegistryValueKind]::MultiString)
            $settingsKey.SetValue('Automatic Add Allowed', [string[]]@('lsadmin'), [Microsoft.Win32.RegistryValueKind]::MultiString)
            $settingsKey.SetValue('Admin Rights Timeout', 15, [Microsoft.Win32.RegistryValueKind]::DWord)
            $settingsKey.SetValue('Remove Admin Rights On Logout', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
            $settingsKey.SetValue('Require Authentication For Privileges', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
            $settingsKey.SetValue('Authentication Mode', 3, [Microsoft.Win32.RegistryValueKind]::DWord)
            $settingsKey.SetValue('Allow Remote Requests', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
            $settingsKey.Flush()

            $expectedDwords = @{
                'Admin Rights Timeout' = 15
                'Remove Admin Rights On Logout' = 1
                'Require Authentication For Privileges' = 1
                'Authentication Mode' = 3
                'Allow Remote Requests' = 1
            }
            foreach ($entry in $expectedDwords.GetEnumerator()) {
                if ([int]$settingsKey.GetValue($entry.Key, -1) -ne $entry.Value) {
                    throw "Registry verification failed for '$($entry.Key)'."
                }
            }
            if (@($settingsKey.GetValue('Allowed Entities')).Count -ne 1 -or
                @($settingsKey.GetValue('Allowed Entities'))[0] -ne 'Users') {
                throw "Registry verification failed for 'Allowed Entities'."
            }
            if (@($settingsKey.GetValue('Automatic Add Allowed')).Count -ne 1 -or
                @($settingsKey.GetValue('Automatic Add Allowed'))[0] -ne 'lsadmin') {
                throw "Registry verification failed for 'Automatic Add Allowed'."
            }
        }
        finally {
            if ($null -ne $settingsKey) { $settingsKey.Dispose() }
        }
    }
    finally {
        $baseKey.Dispose()
    }

    Write-Output 'Make Me Admin 2.4.2 installed and all settings verified successfully.'
    exit $(if ($process.ExitCode -in 1641, 3010) { 3010 } else { 0 })
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
