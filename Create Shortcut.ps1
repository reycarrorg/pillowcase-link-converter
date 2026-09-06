$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationFramework

try {
    $appPath = Join-Path $PSScriptRoot 'dist\PillowcaseLinkConverter.exe'
    $shortcutPath = Join-Path $PSScriptRoot 'Pillowcase Link Converter.lnk'
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw "The app could not be built: $appPath" }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $appPath
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.Description = 'Edit and convert Pillowcase links for JDownloader'
    $shortcut.IconLocation = "$appPath,0"
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
