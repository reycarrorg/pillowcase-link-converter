$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationFramework
Import-Module (Join-Path $PSScriptRoot 'src\PillowcaseLinkConverter.psm1') -Force

$inputPath = Join-Path $PSScriptRoot 'Pillowcase Links.txt'
$unconvertedPath = Join-Path $PSScriptRoot 'Unconverted Links.txt'

try {
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        Set-Content -LiteralPath $inputPath -Value $null -Encoding UTF8
    }

    $sourceText = Get-Content -LiteralPath $inputPath -Raw
    $result = Get-PillowcaseLinkConversion -Text $sourceText
    $persistentLinks = Merge-UnconvertedLinkLog -Path $unconvertedPath -NewLinks $result.UnconvertedLinks

    if ($result.ConvertedLinks.Count -eq 0) {
        $logMessage = if ($result.UnconvertedLinks.Count -gt 0) {
            "`n`n$($result.UnconvertedLinks.Count) unconverted URL(s) were found in this batch. The persistent log contains $($persistentLinks.Count) unique URL(s)."
        }
        elseif ($persistentLinks.Count -gt 0) {
            "`n`nNo new unconverted URLs were found. The persistent log still contains $($persistentLinks.Count) unique URL(s)."
        }
        else {
            ''
        }

        [System.Windows.MessageBox]::Show(
            "No valid Pillowcase landing-page links were found in:`n`n$inputPath`n`nExpected format:`nhttps://pillows.su/f/32-character-file-id$logMessage",
            'Nothing to Convert',
            [System.Windows.MessageBoxButton]::OK,
            [System.Windows.MessageBoxImage]::Information
        ) | Out-Null
        exit 2
    }

    $outputText = $result.ConvertedLinks -join [Environment]::NewLine
    Set-Clipboard -Value $outputText

    $message = "Converted $($result.ConvertedLinks.Count) unique Pillowcase link(s) and copied the API download links to your clipboard.`n`nOpen JDownloader LinkGrabber and press Ctrl+V."

    if ($result.UnconvertedLinks.Count -gt 0) {
        $message += "`n`n$($result.UnconvertedLinks.Count) unconverted URL(s) were found in this batch. The persistent log now contains $($persistentLinks.Count) unique URL(s):`n$unconvertedPath"
    }
    elseif ($persistentLinks.Count -gt 0) {
        $message += "`n`nNo new unconverted URLs were found. The persistent log still contains $($persistentLinks.Count) unique URL(s):`n$unconvertedPath"
    }

    [System.Windows.MessageBox]::Show(
        $message,
        'Pillowcase Links Ready',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Information
    ) | Out-Null
}
catch {
    [System.Windows.MessageBox]::Show(
        "The links could not be converted.`n`n$($_.Exception.Message)",
        'Pillowcase Link Converter Error',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error
    ) | Out-Null
    exit 3
}
