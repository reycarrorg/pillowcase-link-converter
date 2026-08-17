function Get-PillowcaseLinkConversion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Text
    )

    $allLinks = @(
        [regex]::Matches($Text, 'https?://[^\s\]\)\}<>"'']+') |
            ForEach-Object { $_.Value.TrimEnd('.', ',', ';', ':', '!', '?') } |
            Select-Object -Unique
    )

    $convertiblePattern = '^https?://(?:www\.)?pillows\.su/f/[0-9a-fA-F]{32}(?:[/?#].*)?$'

    $convertedLinks = @(
        [regex]::Matches(
            $Text,
            'https?://(?:www\.)?pillows\.su/f/([0-9a-fA-F]{32})(?=$|[\s\]\)\}<>"''/?#])'
        ) |
            ForEach-Object {
                "https://api.pillows.su/api/download/$($_.Groups[1].Value.ToLowerInvariant())"
            } |
            Select-Object -Unique
    )

    $unconvertedLinks = @(
        $allLinks |
            Where-Object { $_ -notmatch $convertiblePattern } |
            Select-Object -Unique
    )

    [pscustomobject]@{
        ConvertedLinks   = $convertedLinks
        UnconvertedLinks = $unconvertedLinks
    }
}

function Merge-UnconvertedLinkLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]] $NewLinks = @()
    )

    $existingLinks = @()
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $existingLinks = @(
            Get-Content -LiteralPath $Path |
                ForEach-Object { $_.Trim() } |
                Where-Object { $_ -match '^https?://' } |
                Select-Object -Unique
        )
    }

    $mergedLinks = @(
        @($existingLinks) + @($NewLinks) |
            Where-Object { $_ -match '^https?://' } |
            Select-Object -Unique
    )

    if ($mergedLinks.Count -gt 0) {
        Set-Content -LiteralPath $Path -Value $mergedLinks -Encoding UTF8
    }

    return $mergedLinks
}

Export-ModuleMember -Function Get-PillowcaseLinkConversion, Merge-UnconvertedLinkLog
