$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\src\PillowcaseLinkConverter.psm1') -Force

$sample = @'
Heading
[https://pillows.su/f/0123456789ABCDEF0123456789ABCDEF](https://pillows.su/f/0123456789ABCDEF0123456789ABCDEF)
http://www.pillows.su/f/fedcba9876543210fedcba9876543210
https://example.com/file.zip
https://example.com/file.zip
'@

$result = Get-PillowcaseLinkConversion -Text $sample

if ($result.ConvertedLinks.Count -ne 2) {
    throw "Expected 2 converted links; got $($result.ConvertedLinks.Count)."
}

if ($result.UnconvertedLinks.Count -ne 1) {
    throw "Expected 1 unconverted link; got $($result.UnconvertedLinks.Count)."
}

if ($result.ConvertedLinks[0] -ne 'https://api.pillows.su/api/download/0123456789abcdef0123456789abcdef') {
    throw 'The first converted URL was incorrect.'
}

if ($result.ConvertedLinks[1] -ne 'https://api.pillows.su/api/download/fedcba9876543210fedcba9876543210') {
    throw 'The second converted URL was incorrect.'
}

if ($result.UnconvertedLinks[0] -ne 'https://example.com/file.zip') {
    throw 'The unconverted URL log was incorrect.'
}

$testLog = Join-Path $env:TEMP "pillowcase-link-converter-test-$([guid]::NewGuid()).txt"
try {
    $firstMerge = Merge-UnconvertedLinkLog -Path $testLog -NewLinks @(
        'https://example.com/old',
        'https://example.com/shared'
    )

    $secondMerge = Merge-UnconvertedLinkLog -Path $testLog -NewLinks @(
        'https://example.com/shared',
        'https://example.com/new'
    )

    if ($firstMerge.Count -ne 2 -or $secondMerge.Count -ne 3) {
        throw 'Persistent log merging or cross-run deduplication failed.'
    }
}
finally {
    Remove-Item -LiteralPath $testLog -Force -ErrorAction SilentlyContinue
}

Write-Host 'All Pillowcase Link Converter tests passed.' -ForegroundColor Green
