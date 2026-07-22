#Requires -Version 5.1

$ErrorActionPreference = 'Stop'
$productCodes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($view in [Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32) {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
    try {
        $uninstallKey = $baseKey.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall')
        if ($null -eq $uninstallKey) { continue }
        try {
            foreach ($subKeyName in $uninstallKey.GetSubKeyNames()) {
                $applicationKey = $uninstallKey.OpenSubKey($subKeyName)
                try {
                    if ($applicationKey.GetValue('DisplayName') -eq 'Make Me Admin' -and
                        $subKeyName -match '^\{[0-9A-Fa-f-]{36}\}$') {
                        [void]$productCodes.Add($subKeyName)
                    }
                }
                finally {
                    if ($null -ne $applicationKey) { $applicationKey.Dispose() }
                }
            }
        }
        finally {
            $uninstallKey.Dispose()
        }
    }
    finally {
        $baseKey.Dispose()
    }
}

foreach ($productCode in $productCodes) {
    $logDirectory = Join-Path $env:ProgramData 'Microsoft\IntuneManagementExtension\Logs'
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
    }
    $logPath = Join-Path $logDirectory 'MakeMeAdmin-uninstall.log'
    $arguments = @('/x', $productCode, '/qn', '/norestart', '/L*v', ('"{0}"' -f $logPath))
    $process = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -notin 0, 1605, 1614, 1641, 3010) {
        throw "Windows Installer returned $($process.ExitCode). See '$logPath'."
    }
}
exit 0
