#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$MsiPath = (Join-Path $PSScriptRoot 'MakeMeAdmin-2.4.2-en-us-x64.msi')
)

$ErrorActionPreference = 'Stop'
$logDirectory = Join-Path $env:ProgramData 'Microsoft\IntuneManagementExtension\Logs'
if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
}
$msiLogPath = Join-Path $logDirectory 'MakeMeAdmin-install.log'
$wrapperLogPath = Join-Path $logDirectory 'MakeMeAdmin-wrapper.log'

function Write-WrapperLog {
    param([string]$Message)
    $line = '{0:u} {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $wrapperLogPath -Value $line
    Write-Output $Message
}

trap {
    Write-WrapperLog "ERROR: $($_.Exception.Message)"
    exit 1
}

if (-not (Test-Path -LiteralPath $MsiPath)) {
    throw "The Make Me Admin MSI was not found at '$MsiPath'."
}

Write-WrapperLog "Wrapper started as '$([Security.Principal.WindowsIdentity]::GetCurrent().Name)'."
$arguments = @('/i', ('"{0}"' -f (Resolve-Path -LiteralPath $MsiPath).Path), '/qn', '/norestart', '/L*v', ('"{0}"' -f $msiLogPath))
$process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -Wait -PassThru
if ($process.ExitCode -notin 0, 1641, 3010) {
    throw "Windows Installer returned $($process.ExitCode). See '$msiLogPath'."
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

Write-WrapperLog 'Installation and registry verification completed successfully.'
exit $(if ($process.ExitCode -in 1641, 3010) { 3010 } else { 0 })
