# Agent entry point for the step index. Builds tools\TestIndex on first use or when its
# sources change, then runs it against the repo root (parent of this tools folder).
#   tools\testindex.ps1 find "open customer dialog" -n 8 --kind when
#   tools\testindex.ps1 check Features\Customer.feature
#   tools\testindex.ps1 stats
$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot 'TestIndex'
$out  = Join-Path $proj 'out'
$exe  = Join-Path $out 'testindex.exe'
$repo = Split-Path $PSScriptRoot -Parent

$needBuild = -not (Test-Path $exe)
if (-not $needBuild) {
    $exeTime = (Get-Item $exe).LastWriteTime
    $needBuild = [bool](Get-ChildItem $proj -File | Where-Object {
        ($_.Extension -in '.cs', '.csproj', '.props', '.targets') -and $_.LastWriteTime -gt $exeTime })
}
if ($needBuild) {
    Write-Host 'testindex: building tool (first run or sources changed)...'
    $log = & dotnet build $proj -c Release -o $out -nologo -v:q "-clp:ErrorsOnly;NoSummary" 2>&1
    if ($LASTEXITCODE -ne 0) { $log | Select-Object -First 20; exit $LASTEXITCODE }
}

& $exe --root $repo @args
exit $LASTEXITCODE
