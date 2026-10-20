# Publishes the Windows release candidate (Stage 17). One command used identically
# by CI (.github/workflows/windows-ci.yml) and locally, so the packaged output is
# reproducible from a pinned recipe. Unpackaged + framework-dependent, x64 — the
# deployment model documented in docs/WINDOWS-RELEASE.md; do not change
# self-contained/MSIX settings here without updating that document first.
param(
    # Relative to the windows/ directory. The publish tree and the SHA-named zip
    # land under it (gitignored), never inside the source or test output trees.
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$windowsDir = Split-Path -Parent $PSScriptRoot

$publishDir = Join-Path $windowsDir "$OutputRoot\release"
$zipDir = Join-Path $windowsDir $OutputRoot

Write-Host "== Publishing (Release, win-x64, framework-dependent) =="
dotnet publish (Join-Path $windowsDir "src\Mitosis.Windows.App\Mitosis.Windows.App.csproj") `
    -c Release -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Sanity: the pieces a clean machine cannot work without, plus the full asset set.
$appExe = Join-Path $publishDir "Mitosis.Windows.App.exe"
$bootstrapDll = Join-Path $publishDir "Microsoft.WindowsAppRuntime.Bootstrap.dll"
$depsJson = Join-Path $publishDir "Mitosis.Windows.App.deps.json"
$runtimeConfig = Join-Path $publishDir "Mitosis.Windows.App.runtimeconfig.json"
foreach ($required in @($appExe, $bootstrapDll, $depsJson, $runtimeConfig)) {
    if (-not (Test-Path $required)) { throw "Publish output is missing $required" }
}
$poses = @(Get-ChildItem (Join-Path $publishDir "Assets") -Filter "pose-*.png")
if ($poses.Count -ne 13) {
    throw "Expected the 13 brand pose assets in the output, found $($poses.Count)"
}

# The artifact must never carry source files, tool caches, tests, or user data:
# everything under %LOCALAPPDATA% is resolved at runtime, never published.
$unexpected = @(Get-ChildItem $publishDir -Recurse |
    Where-Object { $_.Extension -in ".cs", ".csproj", ".swift", ".json.user", ".log" })
if ($unexpected.Count -gt 0) {
    throw "Publish output contains non-runtime files: $($unexpected | Select-Object -First 3)"
}

$sha = (git -C $windowsDir rev-parse --short HEAD).Trim()
$zipPath = Join-Path $zipDir "Mitosis-windows-x64-$sha.zip"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
$zip = Get-Item $zipPath

Write-Host ("== Release candidate: {0} ({1:N0} bytes) ==" -f $zip.Name, $zip.Length)
Write-Host ("   publish tree: {0} files, {1:N0} bytes in Assets" -f
    @(Get-ChildItem $publishDir -Recurse -File).Count,
    ((Get-ChildItem (Join-Path $publishDir "Assets") -Recurse -File | Measure-Object Length -Sum).Sum))
