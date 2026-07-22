#Requires -Version 5.1

$ErrorActionPreference = 'Stop'
try {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $settingsKey = $baseKey.OpenSubKey('SOFTWARE\Sinclair Community College\Make Me Admin')
        if ($null -eq $settingsKey) { exit 1 }
        try {
            $expectedDwords = @{
                'Admin Rights Timeout' = 15
                'Remove Admin Rights On Logout' = 1
                'Require Authentication For Privileges' = 1
                'Authentication Mode' = 3
                'Allow Remote Requests' = 1
            }
            foreach ($entry in $expectedDwords.GetEnumerator()) {
                if ([int]$settingsKey.GetValue($entry.Key, -1) -ne $entry.Value) { exit 1 }
            }
            if ($settingsKey.GetValue('InstalledVersion', '') -ne '2.4.2') { exit 1 }
            if (@($settingsKey.GetValue('Allowed Entities')).Count -ne 1 -or
                @($settingsKey.GetValue('Allowed Entities'))[0] -ne 'Users') { exit 1 }
            if (@($settingsKey.GetValue('Automatic Add Allowed')).Count -ne 1 -or
                @($settingsKey.GetValue('Automatic Add Allowed'))[0] -ne 'lsadmin') { exit 1 }
        }
        finally {
            $settingsKey.Dispose()
        }
    }
    finally {
        $baseKey.Dispose()
    }

    if ((Get-Service -Name 'MakeMeAdmin' -ErrorAction Stop).Status -ne 'Running') { exit 1 }
    Write-Output 'Make Me Admin 2.4.2 is installed, running, and configured.'
    exit 0
}
catch {
    exit 1
}
