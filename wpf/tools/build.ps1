# Agent build wrapper: prints only unique errors (max 25), relative paths, no project suffixes.
#   tools\build.ps1                          -> first *.sln/*.slnx in the repo root
#   tools\build.ps1 Tests\Tests.csproj
#   tools\build.ps1 -MSBuild                 -> use Visual Studio's MSBuild (legacy .NET Framework projects)
param(
    [string]$Target = '',
    [switch]$MSBuild
)
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Target) {
    $Target = (Get-ChildItem $repo -File | Where-Object { $_.Extension -in '.sln', '.slnx' } | Select-Object -First 1).FullName
    if (-not $Target) { 'No .sln found in repo root; pass a target.'; exit 2 }
}

$switches = @('-nologo', '-v:q', '-clp:ErrorsOnly;NoSummary')
if ($MSBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msb = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    $out = & $msb $Target @switches 2>&1
} else {
    $out = & dotnet build $Target @switches 2>&1
}
$code = $LASTEXITCODE

$root = [regex]::Escape($repo + '\')
$errors = $out | ForEach-Object { "$_" } |
    Where-Object { $_ -match ': error ' } |
    ForEach-Object { ($_ -replace '\s+\[[^\]]+proj\]\s*$', '') -replace $root, '' } |
    Sort-Object -Unique

if ($code -eq 0) { 'BUILD OK'; exit 0 }
"BUILD FAILED: $(@($errors).Count) unique error(s)"
$errors | Select-Object -First 25
if (@($errors).Count -eq 0) { $out | Select-Object -Last 15 }
exit $code
