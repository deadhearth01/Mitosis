<#
.SYNOPSIS
    Repeatable UI smoke test for the Windows app: drives the real window through UI
    Automation, captures evidence screenshots, and reports JSON checks.

.DESCRIPTION
    Replaces the throwaway %TEMP% scripts used during the Stage 27 evidence passes.
    Every scenario launches (or reuses) the Release build, resizes the window to a fixed
    client size, acts only through named UI Automation elements (no synthetic clicks on
    unlabelled coordinates), and writes a machine-readable summary so a later run can be
    diffed.

    Scenarios:
      main-window    capture the main window and assert the sidebar lists applications
                     while known component names stay hidden by default
      system-filter  toggle Settings > Advanced > Show system components and prove the
                     list changes both ways, restoring the default before exit
      plan-preview   open the provisioning-plan dry run for a planned profile and prove
                     it renders the refusal with the 'nothing executed' banner
      all            every scenario in one run

    The app is closed at the end unless -KeepOpen is given. Nothing is copied, launched,
    or modified beyond the user's own preference flag, which this script restores.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\tools\ui-smoke.ps1 -Scenario all

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File windows\tools\ui-smoke.ps1 -Scenario main-window -OutDir C:\Temp\shots
#>
[CmdletBinding()]
param(
    [ValidateSet('main-window', 'system-filter', 'plan-preview', 'all')]
    [string]$Scenario = 'main-window',

    [string]$OutDir,

    [string]$ExePath,

    [switch]$KeepOpen,

    [int]$Width = 1180,
    [int]$Height = 960
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $scriptRoot '..\..\docs\evidence\windows-ui'
}
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $scriptRoot '..\src\Mitosis.Windows.App\bin\x64\Release\net10.0-windows10.0.19041.0\Mitosis.exe'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MitosisUiNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetActiveWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
"@

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$TogglePattern = [System.Windows.Automation.TogglePattern]::Pattern

$OutDir = [IO.Path]::GetFullPath($OutDir)
$ExePath = [IO.Path]::GetFullPath($ExePath)
$summaryDir = Join-Path $env:TEMP 'mitosis-ui-smoke'
New-Item -ItemType Directory -Force -Path $OutDir, $summaryDir | Out-Null

$script:checks = New-Object System.Collections.Generic.List[object]
$script:captures = New-Object System.Collections.Generic.List[string]
$script:launched = $false

function Add-Check([string]$Name, [bool]$Ok, [string]$Detail) {
    $script:checks.Add([pscustomobject]@{ name = $Name; ok = $Ok; detail = $Detail })
    $mark = if ($Ok) { 'PASS' } else { 'FAIL' }
    Write-Host "[$mark] $Name - $Detail"
    if (-not $Ok) { $script:failed = $true }
}

function Get-MitosisWindows {
    $result = @()
    foreach ($process in @(Get-Process -Name 'Mitosis' -ErrorAction SilentlyContinue)) {
        $condition = [System.Windows.Automation.PropertyCondition]::new($AE::ProcessIdProperty, $process.Id)
        foreach ($window in $AE::RootElement.FindAll($TS::Children, $condition)) {
            $result += [pscustomobject]@{
                ProcessId = $process.Id
                Handle    = [IntPtr]$window.Current.NativeWindowHandle
                Name      = $window.Current.Name
                Element   = $window
            }
        }
    }
    return $result
}

function Get-WindowShape([IntPtr]$Handle) {
    $rect = New-Object MitosisUiNative+RECT
    $ok = [MitosisUiNative]::DwmGetWindowAttribute($Handle, 9, [ref]$rect, 16)
    if (-not $ok) {
        [MitosisUiNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    }
    return [pscustomobject]@{
        Left   = $rect.Left
        Top    = $rect.Top
        Width  = $rect.Right - $rect.Left
        Height = $rect.Bottom - $rect.Top
        Dpi    = [MitosisUiNative]::GetDpiForWindow($Handle)
    }
}

function Find-Window([string]$Name, [int]$TimeoutSeconds = 30) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $match = @(Get-MitosisWindows | Where-Object { $_.Name -eq $Name })
        if ($match.Count -gt 0) { return $match[0] }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    throw "Window '$Name' did not appear within $TimeoutSeconds s."
}

function Activate-Window([IntPtr]$Handle) {
    $foreground = [MitosisUiNative]::GetForegroundWindow()
    $foregroundThread = [MitosisUiNative]::GetWindowThreadProcessId($foreground, [IntPtr]::Zero)
    $myThread = [MitosisUiNative]::GetCurrentThreadId()
    [MitosisUiNative]::AttachThreadInput($myThread, $foregroundThread, $true) | Out-Null
    # Restore only: minimizing first makes every capture race the restore animation and
    # can sample a zero-sized frame.
    [MitosisUiNative]::ShowWindow($Handle, 9) | Out-Null
    [MitosisUiNative]::BringWindowToTop($Handle) | Out-Null
    [MitosisUiNative]::SetActiveWindow($Handle) | Out-Null
    [MitosisUiNative]::SetForegroundWindow($Handle) | Out-Null
    [MitosisUiNative]::AttachThreadInput($myThread, $foregroundThread, $false) | Out-Null
    Start-Sleep -Milliseconds 500
}

function Park-Pointer {
    # Park the pointer far from the list so no hover fill or scrollbar appears in a
    # capture; this is a pointer move only, never a click.
    [MitosisUiNative]::SetCursorPos(1900, 1070) | Out-Null
}

function Test-InteractiveDesktop {
    # DWM stops composing while the workstation is locked: PrintWindow returns a blank frame
    # and CopyFromScreen fails with "The handle is invalid". UI Automation keeps working, so
    # the behavioural checks still run; only the screenshots are skipped and reported.
    $session = (Get-Process -Id $PID).SessionId
    $lockScreen = @(Get-Process -Name 'LogonUI' -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -eq $session })
    return $lockScreen.Count -eq 0
}

function Save-Capture($Window, [string]$FileName) {
    try {
        return Capture-Window $Window $FileName
    }
    catch {
        Add-Check "capture:$FileName" $false $_.Exception.Message
        return $null
    }
}

function Capture-Window($Window, [string]$FileName) {
    Park-Pointer
    Activate-Window $Window.Handle
    $shape = Get-WindowShape $Window.Handle
    if ($shape.Width -le 0 -or $shape.Height -le 0) {
        Start-Sleep -Milliseconds 600
        $shape = Get-WindowShape $Window.Handle
    }
    if ($shape.Width -le 0 -or $shape.Height -le 0) {
        throw "Window '$($Window.Name)' reported an empty frame ($($shape.Width)x$($shape.Height))."
    }
    $pixelWidth = [int]$shape.Width
    $pixelHeight = [int]$shape.Height
    $bitmap = New-Object System.Drawing.Bitmap -ArgumentList $pixelWidth, $pixelHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $deviceContext = $graphics.GetHdc()
    $printed = [MitosisUiNative]::PrintWindow($Window.Handle, $deviceContext, 2)
    $graphics.ReleaseHdc($deviceContext)

    $colors = @{}
    for ($x = 0; $x -lt $bitmap.Width; $x += [Math]::Max(1, [int]($bitmap.Width / 12))) {
        for ($y = 0; $y -lt $bitmap.Height; $y += [Math]::Max(1, [int]($bitmap.Height / 12))) {
            $colors[$bitmap.GetPixel($x, $y).ToArgb()] = $true
        }
    }
    $method = 'PrintWindow'
    if ($colors.Count -lt 3) {
        try {
            $screen = [System.Drawing.Graphics]::FromImage($bitmap)
            $screen.CopyFromScreen($shape.Left, $shape.Top, 0, 0, [System.Drawing.Size]::new($pixelWidth, $pixelHeight))
            $screen.Dispose()
            $method = 'screen'
        }
        catch {
            $graphics.Dispose()
            $bitmap.Dispose()
            throw ("Could not capture the window: PrintWindow returned no content and the screen " +
                "copy failed ($($_.Exception.Message)). Is the interactive desktop locked or disconnected?")
        }
    }
    $graphics.Dispose()
    $path = Join-Path $OutDir $FileName
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $script:captures.Add($path)
    Write-Host "captured $FileName ($($shape.Width)x$($shape.Height) via $method)"
    return [pscustomobject]@{ Path = $path; Width = $shape.Width; Height = $shape.Height; Method = $method }
}

function Find-Elements($Root, [string]$Name) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($AE::NameProperty, $Name)
    return @($Root.FindAll($TS::Descendants, $condition))
}

function Find-Element($Root, [string]$Name) {
    # Kept for presence checks: any element carrying the accessible name.
    $matches = @(Find-Elements $Root $Name)
    if ($matches.Count -eq 0) { return $null }
    return $matches[0]
}

function Invoke-Element($Root, [string]$Name) {
    $candidates = @(Find-Elements $Root $Name)
    if ($candidates.Count -eq 0) { throw "Element '$Name' not found." }

    # A Button's accessible name also appears on its inner TextBlock, which has no
    # patterns. Prefer whichever candidate really supports Invoke, then SelectionItem.
    foreach ($element in $candidates) {
        $pattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke()
            Start-Sleep -Milliseconds 600
            return
        }
    }
    foreach ($element in $candidates) {
        $pattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $pattern.Select()
            Start-Sleep -Milliseconds 600
            return
        }
    }
    throw "Element '$Name' supports neither Invoke nor SelectionItem."
}

function Find-Toggle($Root, [string]$Name) {
    foreach ($element in @(Find-Elements $Root $Name)) {
        $pattern = $null
        if ($element.TryGetCurrentPattern($TogglePattern, [ref]$pattern)) {
            return [pscustomobject]@{ Element = $element; Pattern = $pattern }
        }
    }
    throw "Toggle '$Name' not found."
}

function Get-ToggleState($Root, [string]$Name) {
    $toggle = Find-Toggle $Root $Name
    return $toggle.Pattern.Current.ToggleState
}

function Set-ToggleState($Root, [string]$Name, [bool]$On) {
    $toggle = Find-Toggle $Root $Name
    $desired = if ($On) { [System.Windows.Automation.ToggleState]::On } else { [System.Windows.Automation.ToggleState]::Off }
    if ($toggle.Pattern.Current.ToggleState -ne $desired) {
        $toggle.Pattern.Toggle()
        Start-Sleep -Milliseconds 700
    }
    return $toggle.Pattern.Current.ToggleState
}

function Get-SidebarRows($Root) {
    $condition = [System.Windows.Automation.PropertyCondition]::new($AE::ControlTypeProperty, $CT::ListItem)
    return @($Root.FindAll($TS::Descendants, $condition) | ForEach-Object { $_.Current.Name })
}

function Focus-Element($Root, [string]$Name) {
    $element = Find-Element $Root $Name
    if ($null -ne $element) {
        $element.SetFocus()
        Start-Sleep -Milliseconds 300
    }
}

function Select-AppRow($Root, [string]$Prefix) {
    # SelectionItem, not a synthetic click: the row keeps keyboard focus semantics.
    $condition = [System.Windows.Automation.PropertyCondition]::new($AE::ControlTypeProperty, $CT::ListItem)
    foreach ($row in $Root.FindAll($TS::Descendants, $condition)) {
        if ($row.Current.Name -like "$Prefix*") {
            $pattern = $null
            if ($row.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
                $pattern.Select()
                Start-Sleep -Milliseconds 900
                return $true
            }
        }
    }
    return $false
}

function Find-NameContaining($Root, [string]$Fragment) {
    # Plain-text matching keeps the script ASCII-only: the banner contains an em dash,
    # which Windows PowerShell 5.1 would mangle when reading this UTF-8 file.
    $all = $Root.FindAll($TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        if ($element.Current.Name -like "*$Fragment*") { return $element }
    }
    return $null
}

function Close-Window($Window) {
    [MitosisUiNative]::PostMessage($Window.Handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 900
}

function Start-App {
    if (@(Get-Process -Name 'Mitosis' -ErrorAction SilentlyContinue).Count -gt 0) {
        Write-Host 'Reusing the running Mitosis instance (the app is single-instance).'
    }
    else {
        if (-not (Test-Path $ExePath)) { throw "Release build not found: $ExePath" }
        $env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet10'
        Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath) | Out-Null
        $script:launched = $true
    }
    $window = Find-Window 'Mitosis' 45
    Start-Sleep -Seconds 2
    return $window
}

function Set-MainWindowSize($Window) {
    [MitosisUiNative]::SetWindowPos($Window.Handle, [IntPtr]::Zero, 100, 100, $Width, $Height, 0x0040) | Out-Null
    Start-Sleep -Milliseconds 900
    $shape = Get-WindowShape $Window.Handle
    Add-Check 'window-size' ($shape.Width -eq $Width -and $shape.Height -eq $Height) `
        "requested ${Width}x${Height}, actual $($shape.Width)x$($shape.Height), dpi $($shape.Dpi)"
}

function Read-PreferenceFlag([string]$Name) {
    $path = Join-Path $env:LOCALAPPDATA 'Mitosis\preferences.json'
    if (-not (Test-Path $path)) { return $null }
    $document = Get-Content -Raw -Path $path | ConvertFrom-Json
    if ($document.PSObject.Properties.Name -contains $Name) { return $document.$Name }
    return $null
}

function Open-Settings($MainWindow) {
    Invoke-Element $MainWindow.Element 'Settings'
    $settings = Find-Window 'Mitosis Settings' 20
    Invoke-Element $settings.Element 'Advanced'
    return $settings
}

$componentSamples = @(
    'AVC Encoder Video Extension',
    'AV1 Video Extension',
    'Microsoft .NET Runtime',
    'Redistributable',
    'Local Experience Pack',
    'App Installer'
)

$failureCount = 0
$result = [pscustomobject][ordered]@{
    scenario   = $Scenario
    startedAt  = (Get-Date).ToUniversalTime().ToString('u')
    finishedAt = $null
    ok         = $null
    checks     = @()
    captures   = @()
}

$mainWindow = $null
try {
    $mainWindow = Start-App
    Set-MainWindowSize $mainWindow

    if (-not (Test-InteractiveDesktop)) {
        Write-Host 'WARNING: the workstation is locked, so screenshots will fail. Behavioural checks still run.'
    }

    if ($Scenario -eq 'main-window' -or $Scenario -eq 'all') {
        Focus-Element $mainWindow.Element 'New Clone'
        $capture = Save-Capture $mainWindow 'smoke-main.png'
        $rows = @(Get-SidebarRows $mainWindow.Element)
        Add-Check 'sidebar-has-apps' ($rows.Count -gt 0) "$($rows.Count) visible rows"
        $visibleComponents = @($rows | Where-Object {
            $row = $_
            @($componentSamples | Where-Object { $row -like "*$_*" }).Count -gt 0
        })
        Add-Check 'components-hidden-by-default' ($visibleComponents.Count -eq 0) `
            $(if ($visibleComponents.Count -eq 0) { 'no known component names in the list' } else { 'visible: ' + ($visibleComponents -join '; ') })
        Add-Check 'hidden-components-hint' ($null -ne (Find-Element $mainWindow.Element 'Show hidden system components')) `
            'footer hint is present'
    }

    if ($Scenario -eq 'system-filter' -or $Scenario -eq 'all') {
        $settings = Open-Settings $mainWindow
        $state = Set-ToggleState $settings.Element 'Show system components' $true
        Add-Check 'toggle-on' ($state -eq [System.Windows.Automation.ToggleState]::On) "state=$state"
        Close-Window $settings
        Start-Sleep -Seconds 2

        $rowsOn = @(Get-SidebarRows $mainWindow.Element)
        $found = @($rowsOn | Where-Object {
            $row = $_
            @($componentSamples | Where-Object { $row -like "*$_*" }).Count -gt 0
        })
        Add-Check 'components-shown-after-toggle' ($found.Count -gt 0) `
            $(if ($found.Count -gt 0) { 'visible: ' + ($found -join '; ') } else { 'no component rows found' })
        Focus-Element $mainWindow.Element 'New Clone'
        $null = Save-Capture $mainWindow 'smoke-system-components-shown.png'

        $settings = Open-Settings $mainWindow
        $state = Set-ToggleState $settings.Element 'Show system components' $false
        Add-Check 'toggle-off' ($state -eq [System.Windows.Automation.ToggleState]::Off) "state=$state"
        Close-Window $settings
        Start-Sleep -Seconds 2

        $rowsOff = @(Get-SidebarRows $mainWindow.Element)
        $stillVisible = @($rowsOff | Where-Object {
            $row = $_
            @($componentSamples | Where-Object { $row -like "*$_*" }).Count -gt 0
        })
        Add-Check 'components-hidden-after-toggle' ($stillVisible.Count -eq 0) `
            $(if ($stillVisible.Count -eq 0) { 'list is back to applications only' } else { 'visible: ' + ($stillVisible -join '; ') })
        Focus-Element $mainWindow.Element 'New Clone'
        $null = Save-Capture $mainWindow 'smoke-system-components-hidden.png'

        Start-Sleep -Milliseconds 800
        $stored = Read-PreferenceFlag 'ShowSystemComponents'
        Add-Check 'preference-restored' ($stored -eq $false) "stored ShowSystemComponents=$stored"
    }

    if ($Scenario -eq 'plan-preview' -or $Scenario -eq 'all') {
        $selected = Select-AppRow $mainWindow.Element 'Android Studio'
        Add-Check 'preview-app-selected' $selected 'a planned profile belongs to Android Studio'

        if ($selected) {
            Invoke-Element $mainWindow.Element 'Preview plan'
            Start-Sleep -Milliseconds 1200

            $title = Find-NameContaining $mainWindow.Element 'Provisioning plan'
            Add-Check 'plan-dialog-open' ($null -ne $title) 'the dialog is in the accessibility tree'

            $banner = Find-NameContaining $mainWindow.Element 'Preview only'
            Add-Check 'plan-banner' ($null -ne $banner) 'the nothing-executed banner is present'

            $refusal = Find-NameContaining $mainWindow.Element 'No provisioning strategy is available'
            Add-Check 'plan-refusal-rendered' ($null -ne $refusal) 'the planner refusal is shown, not a fabricated plan'

            $noSteps = Find-NameContaining $mainWindow.Element 'No steps can be planned'
            Add-Check 'plan-no-false-steps' ($null -ne $noSteps) 'the dialog states that no steps can be planned'

            Invoke-Element $mainWindow.Element 'Close'
            Start-Sleep -Milliseconds 800
            Add-Check 'plan-dialog-closed' ($null -eq (Find-NameContaining $mainWindow.Element 'Preview only')) `
                'dialog dismissed without changing the profile'
        }
    }
}
catch {
    Add-Check 'script' $false ($_.Exception.Message + " | " + $_.ScriptStackTrace)
}
finally {
    try {
        $result.checks = $script:checks.ToArray()
        $result.captures = $script:captures.ToArray()
        $result.finishedAt = (Get-Date).ToUniversalTime().ToString('u')
        $result.ok = (@($script:checks | Where-Object { -not $_.ok }).Count -eq 0)
        $summaryPath = Join-Path $summaryDir ("ui-smoke-{0}.json" -f $Scenario)
        $result | ConvertTo-Json -Depth 5 | Set-Content -Path $summaryPath -Encoding UTF8
        Write-Host "summary: $summaryPath"
        $total = $script:checks.Count
        $broken = @($script:checks | Where-Object { -not $_.ok }).Count
        Write-Host "$total checks, $broken failed"
    }
    catch {
        Write-Host "summary failed: $($_.Exception.Message)"
        Write-Host $_.ScriptStackTrace
    }

    if ($null -ne $mainWindow -and -not $KeepOpen -and $script:launched) {
        try {
            Close-Window $mainWindow
        }
        catch {
            Write-Host "close failed: $($_.Exception.Message)"
            Write-Host $_.ScriptStackTrace
        }
    }
}

if ($result.ok) { exit 0 } else { exit 1 }
