[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$source = Join-Path $root 'src\PillowcaseLinkConverter.App'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$assemblyRoot = "$env:WINDIR\Microsoft.NET\assembly"
function Find-Assembly([string]$area, [string]$name) {
    $file = Get-ChildItem -LiteralPath (Join-Path $assemblyRoot $area) -Filter "$name.dll" -Recurse -ErrorAction Stop | Select-Object -First 1 -ExpandProperty FullName
    if (-not $file) { throw "Required Windows assembly not found: $name" }
    return $file
}
$references = @(
    (Find-Assembly 'GAC_64\PresentationCore' 'PresentationCore'),
    (Find-Assembly 'GAC_MSIL\PresentationFramework' 'PresentationFramework'),
    (Find-Assembly 'GAC_MSIL\WindowsBase' 'WindowsBase'),
    (Find-Assembly 'GAC_MSIL\System.Xaml' 'System.Xaml')
)
$common = @('/nologo', '/platform:anycpu', '/optimize+') + ($references | ForEach-Object { '/reference:' + $_ })
$iconArgument = '/win32icon:' + (Join-Path $root 'assets\PillowcaseLinkConverter.ico')
& $compiler @common '/target:winexe' $iconArgument ('/out:' + (Join-Path $dist 'PillowcaseLinkConverter.exe')) ('/win32manifest:' + (Join-Path $source 'app.manifest')) (Join-Path $source 'Program.cs') (Join-Path $source 'Core.cs') (Join-Path $source 'MainWindow.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Host "Built: $(Join-Path $dist 'PillowcaseLinkConverter.exe')"
if ($Test) {
    & $compiler @common '/target:exe' ('/out:' + (Join-Path $dist 'PillowcaseLinkConverter.Tests.exe')) (Join-Path $source 'Core.cs') (Join-Path $root 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $dist 'PillowcaseLinkConverter.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
