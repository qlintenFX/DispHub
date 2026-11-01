# Starts a built DispHub with empty settings, opens every page, saves a screenshot of each and fails if it logs an
# error or doesn't quit cleanly. CI runs it on Windows Server 2022, which has Windows 10's code base.
#
#   pwsh build/smoke-test.ps1 -Exe src/DispHub/bin/Release/net10.0-windows10.0.19041.0/DispHub.exe -Out smoke
#
# It quits a running DispHub (as --fresh does), so don't run it while you're using one.
param(
    [Parameter(Mandatory)] [string]$Exe,
    [string]$Out = "smoke",
    [string]$Flags = "--simulate-monitor --simulate-battery"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Smoke {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  public struct RECT { public int L, T, R, B; }
}
"@
[void][Smoke]::SetProcessDpiAwarenessContext([IntPtr](-4))
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$failures = [System.Collections.Generic.List[string]]::new()

function Cond($prop, $value) { [System.Windows.Automation.PropertyCondition]::new($prop, $value) }
function Windows-Of([int]$id) { $AE::RootElement.FindAll($TS::Children, (Cond $AE::ProcessIdProperty $id)) }
function Wait-For([scriptblock]$test, [int]$seconds) {
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    while ([DateTime]::UtcNow -lt $until) { $r = & $test; if ($r) { return $r }; Start-Sleep -Milliseconds 500 }
    return $null
}
function Capture($element, [string]$name) {
    $h = [IntPtr]$element.Current.NativeWindowHandle
    $r = [Smoke+RECT]::new(); [void][Smoke]::GetWindowRect($h, [ref]$r)
    $bmp = [System.Drawing.Bitmap]::new([Math]::Max(1, $r.R - $r.L), [Math]::Max(1, $r.B - $r.T))
    $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
    [void][Smoke]::PrintWindow($h, $dc, 2); $g.ReleaseHdc($dc); $g.Dispose()
    $bmp.Save((Join-Path $Out "$name.png")); $bmp.Dispose()
}

$os = [Environment]::OSVersion.Version
"Windows $os"
$p = Start-Process $Exe -ArgumentList "--fresh $Flags" -PassThru

# The first-run walkthrough comes first; close it to reach the main window.
$walkthrough = Wait-For { Windows-Of $p.Id | Select-Object -First 1 } 60
if (-not $walkthrough) { throw "No window within 60 s" }
Start-Sleep -Seconds 2
Capture $walkthrough "01-walkthrough"
foreach ($w in (Windows-Of $p.Id)) { try { $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch {} }

$main = Wait-For { Windows-Of $p.Id | Where-Object { $_.Current.Name -eq "DispHub" -and $_.Current.BoundingRectangle.Width -gt 400 } | Select-Object -First 1 } 30
if (-not $main) { throw "No main window after closing the walkthrough" }
Start-Sleep -Seconds 1
"Main window: $($main.Current.BoundingRectangle)"

$listItem = Cond $AE::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)
$index = 2
foreach ($page in "Home", "Profiles", "Schedule", "Apps", "Displays", "Settings", "About") {
    # Menu items read as the pages' titles (English on the runner).
    $nav = $main.FindAll($TS::Descendants, $listItem) |
    Where-Object { $_.Current.Name -eq $page } | Select-Object -First 1
    if (-not $nav) { $failures.Add("No $page page in the menu"); continue }
    $nav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
    Capture $main ("{0:d2}-{1}" -f $index++, $page.ToLowerInvariant())
}

$log = Get-ChildItem (Join-Path $env:TEMP "DispHub-fresh\logs") -Filter *.log | Sort-Object LastWriteTime | Select-Object -Last 1
Copy-Item $log.FullName $Out
$lines = Get-Content $log.FullName
$lines | Where-Object { $_ -match " (WARN|ERRO|CRIT) " } | ForEach-Object { "log: $_" }
$errors = @($lines | Where-Object { $_ -match " (ERRO|CRIT) " })
if ($errors.Count -gt 0) { $failures.Add("$($errors.Count) error(s) in the log") }

# Quit as a user would from the tray: colours put back, process gone.
& $Exe --exit
if (-not $p.WaitForExit(20000)) { $failures.Add("Didn't quit within 20 s of --exit"); $p.Kill() }

if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }; exit 1 }
"Smoke test passed: screenshots and log in $Out"
