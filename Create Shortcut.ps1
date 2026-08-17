$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationFramework

try {
    $runnerPath = Join-Path $PSScriptRoot 'Run Converter.ps1'
    $shortcutPath = Join-Path $PSScriptRoot 'Pillowcase Link Converter.lnk'
    $powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

    if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
        throw "The converter script is missing: $runnerPath"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Pillowcase Links.txt'))) {
        Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Pillowcase Links.txt') -Value $null -Encoding UTF8
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $powershellPath
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $runnerPath + '"'
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.Description = 'Convert Pillowcase landing-page links and copy API download links to the clipboard'
    $shortcut.IconLocation = "$powershellPath,0"
    $shortcut.Save()

    [System.Windows.MessageBox]::Show(
        "Shortcut created successfully:`n`n$shortcutPath",
        'Pillowcase Link Converter',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Information
    ) | Out-Null
}
catch {
    [System.Windows.MessageBox]::Show(
        "The shortcut could not be created.`n`n$($_.Exception.Message)",
        'Shortcut Creation Error',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error
    ) | Out-Null
    exit 1
}
