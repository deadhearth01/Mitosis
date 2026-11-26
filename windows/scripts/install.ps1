# Per-user installer/uninstaller for the unpackaged Windows build (Stage 26).
#
# Design constraints, all deliberate:
#  * Per-user only. Everything is written under the current user's profile
#    (%LOCALAPPDATA%, %APPDATA%, HKCU); no elevation, no machine-wide state, no
#    PATH change, no service, no scheduled task.
#  * The archive is the install source. This script ships in the release zip and
#    is copied into the install folder so the uninstall entry can call it back
#    with -Uninstall. There is no network access, no telemetry, and no download:
#    missing prerequisites are reported, never fetched or installed.
#  * Uninstall removes exactly the files recorded at install time. Files the user
#    later adds to the folder are kept, and profile data under UserDataRoot is
#    kept unless -RemoveUserData is passed explicitly.
#  * -DryRun prints the plan and changes nothing. -NoShellIntegration skips the
#    Start-menu shortcut and the HKCU uninstall entry (tests and portable use).
#
# Exit codes: 0 success, 1 usage/preflight failure, 2 refused (unsafe path,
# already installed, app running, missing install marker).
#
# See docs/WINDOWS-INSTALL.md for the user-facing install/uninstall story and
# docs/WINDOWS-RELEASE.md for the deployment model.

param(
    # Folder that contains Mitosis.exe. Defaults to this script's folder, which
    # is the archive root during install and the install folder during uninstall.
    [string]$Source = $PSScriptRoot,
    # Per-user install location. Never a machine-wide Program Files path.
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\Mitosis'),
    # Runtime data (profiles, logs, preferences) owned by the app itself.
    [string]$UserDataRoot = (Join-Path $env:LOCALAPPDATA 'Mitosis'),
    [switch]$Uninstall,
    [switch]$RemoveUserData,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$NoShellIntegration,
    [switch]$SkipPrerequisiteCheck,
    [switch]$StrictPrerequisites,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$MarkerName = '.mitosis-install.json'
$ProductName = 'Mitosis'
$MinAppRuntime = [version]'2.5.0.0'
# Windows App SDK 2.x generation: the framework package is registered per user
# under a generation-scoped name (observed: Microsoft.WindowsAppRuntime.2).
$AppRuntimePackagePrefix = 'Microsoft.WindowsAppRuntime.2'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mitosis'

function Write-Step([string]$Message) {
    if (-not $Quiet) { Write-Host "== $Message ==" }
}

function Write-Info([string]$Message) {
    if (-not $Quiet) { Write-Host "   $Message" }
}

function Write-Finding([string]$Message) {
    if (-not $Quiet) { Write-Host $Message }
}

function Resolve-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path is empty' }
    if ([System.IO.Path]::IsPathRooted($Path)) {
        $full = [System.IO.Path]::GetFullPath($Path)
    } else {
        $full = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
    }
    return $full.TrimEnd('\')
}

function Test-IsUnder([string]$Child, [string]$Parent) {
    $c = Resolve-FullPath $Child
    $p = Resolve-FullPath $Parent
    if ([string]::Equals($c, $p, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    return $c.StartsWith($p + '\', [System.StringComparison]::OrdinalIgnoreCase)
}

# Refuse locations where a recursive delete could destroy unrelated data. This
# is a guard, not a permission system: the marker file check below is what
# proves the folder belongs to this installer.
function Assert-SafeTargetDirectory([string]$Path, [string]$What) {
    $full = Resolve-FullPath $Path
    $segments = $full.Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Count -le 1) {
        throw "$What '$full' is a drive root; refusing"
    }
    # Two segments below the drive (C:\Tools\Mitosis) are required so a typo
    # cannot target C:\Mitosis or a top-level folder holding other software.
    if ($segments.Count -lt 3) {
        throw "$What '$full' is too close to the drive root; refusing"
    }
    $forbidden = @($env:USERPROFILE, $env:LOCALAPPDATA, $env:APPDATA, $env:ProgramData,
        $env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:SystemRoot 'System32'))
    foreach ($candidate in $forbidden) {
        if ($candidate -and [string]::Equals($full, (Resolve-FullPath $candidate),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "$What '$full' is a protected system/profile folder; refusing"
        }
    }
    return $full
}

function Get-SourceIdentity([string]$Directory) {
    $exe = Join-Path $Directory 'Mitosis.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw "The install source '$Directory' does not contain Mitosis.exe"
    }
    $info = (Get-Item -LiteralPath $exe).VersionInfo
    $fileVersion = $info.FileVersion
    $productVersion = $info.ProductVersion
    if (-not $fileVersion) { $fileVersion = $null }
    if (-not $productVersion) { $productVersion = $null }
    $shortVersion = 'unknown'
    if ($fileVersion -match '^(\d+\.\d+\.\d+)\.\d+$') { $shortVersion = $Matches[1] }
    return [pscustomobject]@{
        Executable     = $exe
        FileVersion    = $fileVersion
        ProductVersion = $productVersion
        ShortVersion   = $shortVersion
    }
}

# .NET 10 x64 runtime: the app is framework-dependent (Microsoft.NETCore.App
# 10.0), so a machine without it cannot start. The check mirrors what the
# apphost itself uses when it resolves the framework: the machine-wide install
# recorded by the runtime installer, plus DOTNET_ROOT when set. A private
# user-local install (dotnet-install scripts) is reported separately because the
# apphost does not search it: it only works when DOTNET_ROOT points at it, so
# calling it a plain success would be a false claim.
function Get-VersionsUnder([string]$Root) {
    if (-not $Root) { return @() }
    $shared = Join-Path $Root 'shared\Microsoft.NETCore.App'
    if (-not (Test-Path -LiteralPath $shared)) { return @() }
    return @(Get-ChildItem -LiteralPath $shared -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.' } | ForEach-Object { $_.Name })
}

function Get-DotnetRuntimeReport {
    $machineRoot = $null
    try {
        $recorded = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost' `
            -Name Path -ErrorAction Stop).Path
        if ($recorded) { $machineRoot = $recorded.TrimEnd('\') }
    } catch {
        $machineRoot = $null
    }
    if (-not $machineRoot -and $env:ProgramFiles) {
        $machineRoot = Join-Path $env:ProgramFiles 'dotnet'
    }

    $usableRoots = @($machineRoot)
    if ($env:DOTNET_ROOT) { $usableRoots += $env:DOTNET_ROOT }
    $usableRoots = @($usableRoots | Where-Object { $_ } | Select-Object -Unique)

    $privateRoots = @()
    if ($env:LOCALAPPDATA) {
        $privateRoots += (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet')
        $microsoft = Join-Path $env:LOCALAPPDATA 'Microsoft'
        if (Test-Path -LiteralPath $microsoft) {
            $privateRoots += @(Get-ChildItem -LiteralPath $microsoft -Directory -Filter 'dotnet*' `
                -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
        }
    }
    if ($env:USERPROFILE) { $privateRoots += (Join-Path $env:USERPROFILE '.dotnet') }
    $privateRoots = @($privateRoots | Where-Object { $_ } | Select-Object -Unique | Where-Object {
            $candidate = $_.TrimEnd('\')
            -not ($usableRoots | Where-Object {
                    [string]::Equals((Resolve-FullPath $_), (Resolve-FullPath $candidate),
                        [System.StringComparison]::OrdinalIgnoreCase)
                })
        })

    $usableVersions = @($usableRoots | ForEach-Object { Get-VersionsUnder $_ } |
        Select-Object -Unique | Sort-Object { [version]$_ })
    $privateVersions = @($privateRoots | ForEach-Object { Get-VersionsUnder $_ } |
        Select-Object -Unique | Sort-Object { [version]$_ })

    return [pscustomobject]@{
        Usable          = ($usableVersions.Count -gt 0)
        UsableVersions  = $usableVersions
        PrivateOnly     = ($usableVersions.Count -eq 0 -and $privateVersions.Count -gt 0)
        PrivateVersions = $privateVersions
        PrivateRoots    = @($privateRoots | Where-Object { (Get-VersionsUnder $_).Count -gt 0 })
    }
}

# Windows App Runtime: probe with the same bootstrapper the app uses, from the
# archive's own Microsoft.WindowsAppRuntime.Bootstrap.dll. That is authoritative
# (it is exactly the call that fails at startup) and avoids guessing package
# names. A heuristic per-user package check is the fallback when the bootstrap
# DLL cannot be loaded, e.g. a payload missing the DLL.
function Get-AppRuntimeReport([string]$BootstrapDll) {
    $probeNote = $null
    if (Test-Path -LiteralPath $BootstrapDll) {
        try {
            $escaped = (Resolve-FullPath $BootstrapDll).Replace('"', '""')
            $typeDefinition = @"
using System;
using System.Runtime.InteropServices;

public static class MitosisAppRuntimeProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PackageVersion
    {
        public ushort Revision;
        public ushort Build;
        public ushort Minor;
        public ushort Major;
    }

    [DllImport(@"$escaped", CharSet = CharSet.Unicode)]
    public static extern int MddBootstrapInitialize(uint majorMinorVersion, string versionTag, PackageVersion minVersion);

    [DllImport(@"$escaped")]
    public static extern void MddBootstrapShutdown();
}
"@
            Add-Type -TypeDefinition $typeDefinition -ErrorAction Stop
            $minimum = New-Object MitosisAppRuntimeProbe+PackageVersion
            $minimum.Major = 2
            $minimum.Minor = 5
            $hr = [MitosisAppRuntimeProbe]::MddBootstrapInitialize(0x00020005, $null, $minimum)
            if ($hr -eq 0) {
                [MitosisAppRuntimeProbe]::MddBootstrapShutdown()
                return [pscustomobject]@{ Found = $true; Detail = 'bootstrapper initialized'; Note = $null }
            }
            return [pscustomobject]@{
                Found  = $false
                Detail = ('bootstrapper returned HRESULT 0x{0:X8}' -f ([uint32]$hr))
                Note   = $null
            }
        } catch {
            $probeNote = "bootstrap probe unavailable ($($_.Exception.Message))"
        }
    } else {
        $probeNote = 'bootstrap DLL not present in the install source'
    }

    $packages = @()
    try {
        $packages = @(Get-AppxPackage -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $AppRuntimePackagePrefix -or
                $_.Name -like ($AppRuntimePackagePrefix + '.*') })
    } catch {
        $packages = @()
    }
    $installed = @($packages | Where-Object {
            try { [version]$_.Version -ge $MinAppRuntime } catch { $false }
        } | Sort-Object { [version]$_.Version } -Descending)
    if ($installed.Count -gt 0) {
        return [pscustomobject]@{
            Found  = $true
            Detail = "package $($installed[0].Name) $($installed[0].Version)"
            Note   = $probeNote
        }
    }
    return [pscustomobject]@{ Found = $false; Detail = 'no matching framework package'; Note = $probeNote }
}

function Get-PrerequisiteFindings([string]$SourceDirectory) {
    $findings = New-Object System.Collections.Generic.List[object]
    if (-not [Environment]::Is64BitOperatingSystem) {
        $findings.Add([pscustomobject]@{
                Level = 'error'
                Text  = 'Mitosis is x64-only; this is a 32-bit Windows installation.'
            })
    }
    if ([Environment]::OSVersion.Version.Build -lt 17763) {
        $findings.Add([pscustomobject]@{
                Level = 'warning'
                Text  = ('Windows 10 version 1809 (build 17763) or newer is required; this build is {0}.' -f
                    [Environment]::OSVersion.Version.Build)
            })
    }

    $dotnet = Get-DotnetRuntimeReport
    if ($dotnet.Usable) {
        $findings.Add([pscustomobject]@{
                Level = 'ok'
                Text  = ('Microsoft.NETCore.App 10 x64 runtime found ({0}).' -f
                    ($dotnet.UsableVersions -join ', '))
            })
    } elseif ($dotnet.PrivateOnly) {
        $findings.Add([pscustomobject]@{
                Level = 'warning'
                Text  = (('Microsoft.NETCore.App 10 was found only in a user-local .NET install ({0}). ' +
                    'The apphost uses the machine-wide runtime or DOTNET_ROOT, so install the ' +
                    '.NET 10 Desktop Runtime (x64) or set DOTNET_ROOT={1}.') -f
                    ($dotnet.PrivateVersions -join ', '), $dotnet.PrivateRoots[0])
            })
    } else {
        $findings.Add([pscustomobject]@{
                Level = 'warning'
                Text  = ('Microsoft.NETCore.App 10 x64 runtime was not found in the usual locations. ' +
                    'Install the .NET 10 Desktop Runtime (x64) before starting Mitosis.')
            })
    }

    $appRuntime = Get-AppRuntimeReport (Join-Path $SourceDirectory 'Microsoft.WindowsAppRuntime.Bootstrap.dll')
    if ($appRuntime.Found) {
        $findings.Add([pscustomobject]@{
                Level = 'ok'
                Text  = ('Windows App Runtime 2.5+ found ({0}).' -f $appRuntime.Detail)
            })
    } else {
        $findings.Add([pscustomobject]@{
                Level = 'warning'
                Text  = ('Windows App Runtime 2.5+ was not found ({0}). Install it before starting Mitosis.' -f
                    $appRuntime.Detail)
            })
    }
    if ($appRuntime.Note) {
        $findings.Add([pscustomobject]@{ Level = 'note'; Text = $appRuntime.Note })
    }
    return $findings
}

function Get-PlanFiles([string]$SourceDirectory) {
    $sourceLength = (Resolve-FullPath $SourceDirectory).Length
    $files = Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File -Force |
        Sort-Object FullName
    $relative = foreach ($file in $files) {
        $file.FullName.Substring($sourceLength).TrimStart('\')
    }
    return @($relative)
}

function Test-InstalledAppRunning([string]$Root) {
    $processes = @(Get-Process -Name 'Mitosis' -ErrorAction SilentlyContinue)
    foreach ($process in $processes) {
        $path = $null
        try { $path = $process.Path } catch { $path = $null }
        if ($path -and (Test-IsUnder $path $Root)) { return $process }
    }
    return $null
}

function Read-InstallMarker([string]$Root) {
    $path = Join-Path $Root $MarkerName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    try {
        return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
    } catch {
        return 'invalid'
    }
}

function Write-InstallMarker([string]$Root, $Identity, [string[]]$Files, [string]$ShortcutPath) {
    $marker = [ordered]@{
        Product        = $ProductName
        Version        = $Identity.ShortVersion
        FileVersion    = $Identity.FileVersion
        ProductVersion = $Identity.ProductVersion
        InstalledAt    = (Get-Date).ToUniversalTime().ToString('o')
        InstallRoot    = (Resolve-FullPath $Root)
        ShortcutPath   = $ShortcutPath
        Files          = @($Files)
    }
    $json = $marker | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText((Join-Path $Root $MarkerName), $json + "`r`n",
        (New-Object System.Text.UTF8Encoding($false)))
}

function Install-Shortcut([string]$Root, [string]$Executable) {
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $shortcutPath = Join-Path $programs 'Mitosis.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $Executable
    $shortcut.WorkingDirectory = $Root
    $shortcut.IconLocation = "$Executable,0"
    $shortcut.Description = 'Mitosis'
    $shortcut.Save()
    return $shortcutPath
}

function Register-UninstallEntry([string]$Root, $Identity) {
    $exe = Join-Path $Root 'Mitosis.exe'
    $script = Join-Path $Root 'install.ps1'
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $uninstall = '"{0}" -NoProfile -ExecutionPolicy Bypass -File "{1}" -Uninstall' -f
        $powershell, $script
    $quietUninstall = $uninstall + ' -Quiet'
    if (-not (Test-Path -LiteralPath $UninstallKey)) {
        New-Item -Path $UninstallKey -Force | Out-Null
    }
    $version = if ($Identity.ShortVersion -ne 'unknown') { $Identity.ShortVersion } else { $null }
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'DisplayName' -Value $ProductName
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'Publisher' -Value $ProductName
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'DisplayIcon' -Value "$exe,0"
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'InstallLocation' -Value $Root
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'UninstallString' -Value $uninstall
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'QuietUninstallString' -Value $quietUninstall
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'NoModify' -Value 1 -Type DWord
    Set-ItemProperty -LiteralPath $UninstallKey -Name 'NoRepair' -Value 1 -Type DWord
    if ($version) {
        Set-ItemProperty -LiteralPath $UninstallKey -Name 'DisplayVersion' -Value $version
    }
}

function Invoke-Install {
    $root = Assert-SafeTargetDirectory $InstallRoot 'InstallRoot'
    $identity = Get-SourceIdentity $Source
    $sourceFull = Resolve-FullPath $Source
    $rootExists = Test-Path -LiteralPath $root
    $marker = Read-InstallMarker $root

    if ($marker -eq 'invalid') {
        Write-Finding "The install folder '$root' has an unreadable $MarkerName; refusing to touch it."
        exit 2
    }
    if ($marker -and -not $Force) {
        Write-Finding ("Mitosis {0} is already installed at '{1}'. Pass -Force to replace it." -f
            $marker.Version, $root)
        exit 2
    }
    if (-not $marker -and $rootExists) {
        $existing = @(Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue)
        if ($existing.Count -gt 0) {
            Write-Finding (("The install folder '{0}' already exists and was not created by Mitosis. " +
                'Pass -Force to overwrite it, or choose another -InstallRoot.') -f $root)
            exit 2
        }
    }
    $running = Test-InstalledAppRunning $root
    if ($running) {
        Write-Finding ("Mitosis is running from '{0}' (process {1}). Close it and try again." -f
            $root, $running.Id)
        exit 2
    }

    $findings = @()
    if (-not $SkipPrerequisiteCheck) {
        $findings = @(Get-PrerequisiteFindings $sourceFull)
        foreach ($finding in $findings) {
            switch ($finding.Level) {
                'ok' { if (-not $Quiet) { Write-Host "   OK:   $($finding.Text)" } }
                'warning' { Write-Host "   WARN: $($finding.Text)" }
                'note' { if (-not $Quiet) { Write-Host "   note: $($finding.Text)" } }
                'error' { Write-Host "   FAIL: $($finding.Text)" }
            }
        }
        $blocking = @($findings | Where-Object { $_.Level -eq 'error' })
        $missing = @($findings | Where-Object { $_.Level -eq 'warning' })
        if ($blocking.Count -gt 0 -or ($StrictPrerequisites -and $missing.Count -gt 0)) {
            Write-Finding 'Prerequisites are not satisfied; nothing was installed.'
            exit 1
        }
    }

    $files = @(Get-PlanFiles $sourceFull)
    if ($files.Count -eq 0) {
        Write-Finding "The install source '$sourceFull' contains no files."
        exit 1
    }
    $shortcutPath = $null
    if (-not $NoShellIntegration) {
        $shortcutPath = Join-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') 'Mitosis.lnk'
    }

    Write-Step ("Install Mitosis {0}" -f $identity.ShortVersion)
    Write-Info "from:  $sourceFull"
    Write-Info "to:    $root"
    Write-Info ("files: {0}" -f $files.Count)
    if ($shortcutPath) {
        Write-Info "shortcut: $shortcutPath"
        Write-Info "uninstall entry: $UninstallKey"
    } else {
        Write-Info 'shell integration: skipped (-NoShellIntegration)'
    }

    if ($DryRun) {
        Write-Info 'dry run: no files or settings were changed'
        exit 0
    }

    if (-not $rootExists) {
        New-Item -ItemType Directory -Path $root -Force | Out-Null
    }
    foreach ($relative in $files) {
        $target = Join-Path $root $relative
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        Copy-Item -LiteralPath (Join-Path $sourceFull $relative) -Destination $target -Force
    }

    if ($shortcutPath) {
        try {
            $shortcutPath = Install-Shortcut $root (Join-Path $root 'Mitosis.exe')
            Register-UninstallEntry $root $identity
        } catch {
            Write-Finding (("Shell integration failed ({0}). The app was installed but will not " +
                'appear in the Start menu or Apps list.') -f $_.Exception.Message)
            $shortcutPath = $null
        }
    }

    # The marker is written last: a half-copied folder never looks installed, and
    # uninstall refuses folders without it.
    Write-InstallMarker $root $identity $files $shortcutPath

    Write-Step 'Installed'
    Write-Info ("Mitosis {0} installed at {1}" -f $identity.ShortVersion, $root)
    Write-Info "start: $(Join-Path $root 'Mitosis.exe')"
    Write-Info ("uninstall: {0} -Uninstall" -f (Join-Path $root 'install.ps1'))
    Write-Info ("profile data stays in {0}; uninstall keeps it unless -RemoveUserData is passed" -f
        (Resolve-FullPath $UserDataRoot))
    exit 0
}

function Invoke-Uninstall {
    $root = Assert-SafeTargetDirectory $InstallRoot 'InstallRoot'
    $marker = Read-InstallMarker $root
    if ($null -eq $marker) {
        Write-Finding (("No Mitosis install marker was found in '{0}'. Refusing to remove " +
            'a folder this installer does not own.') -f $root)
        exit 2
    }
    if ($marker -eq 'invalid') {
        Write-Finding "The install marker in '$root' is unreadable. Refusing to remove the folder."
        exit 2
    }
    if ($marker.Product -ne $ProductName) {
        Write-Finding ("The install marker in '{0}' belongs to '{1}', not Mitosis. Refusing." -f
            $root, $marker.Product)
        exit 2
    }
    if ($marker.InstallRoot -and
        -not [string]::Equals((Resolve-FullPath $marker.InstallRoot), $root,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Finding ("The install marker names '{0}' but the uninstall target is '{1}'. Refusing." -f
            $marker.InstallRoot, $root)
        exit 2
    }
    $running = Test-InstalledAppRunning $root
    if ($running) {
        Write-Finding ("Mitosis is running from '{0}' (process {1}). Close it and try again." -f
            $root, $running.Id)
        exit 2
    }

    $recorded = @($marker.Files)
    $existing = @($recorded | Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf })
    $userDataPath = Resolve-FullPath $UserDataRoot
    $removeData = [bool]$RemoveUserData

    Write-Step ("Uninstall Mitosis {0}" -f $marker.Version)
    Write-Info "from:  $root"
    Write-Info ("files: {0} recorded, {1} present" -f $recorded.Count, $existing.Count)
    if ($marker.ShortcutPath) { Write-Info "shortcut: $($marker.ShortcutPath) (removed)" }
    if (-not $NoShellIntegration) { Write-Info "uninstall entry: $UninstallKey (removed)" }
    if ($removeData) {
        Write-Info "profile data: $userDataPath (DELETED, -RemoveUserData was passed)"
    } else {
        Write-Info "profile data: $userDataPath (kept; pass -RemoveUserData to delete it)"
    }

    if ($DryRun) {
        Write-Info 'dry run: nothing was changed'
        exit 0
    }

    if ($marker.ShortcutPath -and (Test-Path -LiteralPath $marker.ShortcutPath -PathType Leaf)) {
        Remove-Item -LiteralPath $marker.ShortcutPath -Force -ErrorAction SilentlyContinue
    }
    if ((Test-Path -LiteralPath $UninstallKey) -and -not $NoShellIntegration) {
        $installLocation = $null
        try {
            $installLocation = (Get-ItemProperty -LiteralPath $UninstallKey -Name InstallLocation).InstallLocation
        } catch { $installLocation = $null }
        if (-not $installLocation -or
            [string]::Equals((Resolve-FullPath $installLocation), $root,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $UninstallKey -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $removed = 0
    foreach ($relative in $recorded) {
        $candidate = Join-Path $root $relative
        if (-not (Test-IsUnder $candidate $root) -or
            [string]::Equals((Resolve-FullPath $candidate), (Join-Path $root $MarkerName),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Remove-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $candidate)) { $removed++ }
        }
    }
    Remove-Item -LiteralPath (Join-Path $root $MarkerName) -Force -ErrorAction SilentlyContinue

    # Remove directories that became empty, deepest first. The root itself is
    # only removed when it is empty, so user files are never deleted.
    $directories = @(Get-ChildItem -LiteralPath $root -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Sort-Object { $_.FullName.Length } -Descending)
    foreach ($directory in $directories) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force -ErrorAction SilentlyContinue).Count -eq 0) {
            Remove-Item -LiteralPath $directory.FullName -Force -ErrorAction SilentlyContinue
        }
    }
    $leftover = @(Get-ChildItem -LiteralPath $root -Force -ErrorAction SilentlyContinue)
    if ($leftover.Count -eq 0) {
        Remove-Item -LiteralPath $root -Force -ErrorAction SilentlyContinue
    }

    if ($removeData -and (Test-Path -LiteralPath $userDataPath)) {
        Assert-SafeTargetDirectory $userDataPath 'UserDataRoot' | Out-Null
        Remove-Item -LiteralPath $userDataPath -Recurse -Force
        Write-Info "profile data removed: $userDataPath"
    }

    Write-Step 'Uninstalled'
    Write-Info ("removed {0} file(s) from {1}" -f $removed, $root)
    if (Test-Path -LiteralPath $root) {
        Write-Finding ("The install folder still contains files Mitosis did not create; they were " +
            "kept: $root")
        foreach ($item in $leftover | Select-Object -First 5) {
            Write-Info ("  kept: {0}" -f $item.Name)
        }
    }
    if (-not $removeData -and (Test-Path -LiteralPath $userDataPath)) {
        Write-Info ("profile data kept at {0}; pass -RemoveUserData to delete it" -f $userDataPath)
    }
    exit 0
}

if ($RemoveUserData -and -not $Uninstall) {
    Write-Finding '-RemoveUserData is only valid together with -Uninstall.'
    exit 1
}

try {
    if ($Uninstall) {
        Invoke-Uninstall
    } else {
        Invoke-Install
    }
} catch {
    Write-Finding "Error: $($_.Exception.Message)"
    exit 1
}
