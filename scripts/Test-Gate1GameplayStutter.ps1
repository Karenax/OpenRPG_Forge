[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RuntimeBinary,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PresentMonPath,

    [ValidateSet("AutoNoVsync", "FifoDiagnostic")]
    [string]$PresentationRoute = "AutoNoVsync"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$presentMonVersion = "2.4.1"
$presentMonBytes = 927304L
$presentMonSha256 = "d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34"
$readyFileName = "gate1-gameplay-stutter.ready.json"
$signalFileName = "gate1-gameplay-stutter.start.signal.json"
$hostBoundaryFileName = "gate1-gameplay-stutter.host-boundary.json"
$ledgerFileName = "gate1-gameplay-stutter.json"
$validationFileName = "gate1-gameplay-stutter.validation.json"
$fifoDiagnosticMarkerFileName = "NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC.json"
$runtimeTimeoutSeconds = 120
$readyTimeoutSeconds = 45
$presentMonReadyTimeoutSeconds = 20
$presentMonExitTimeoutSeconds = 15
$drainTimeoutMilliseconds = 5000
$expectedPresentMonColumns = @(
    "Application",
    "ProcessID",
    "SwapChainAddress",
    "PresentRuntime",
    "SyncInterval",
    "PresentFlags",
    "AllowsTearing",
    "PresentMode",
    "CPUStartQPC",
    "FrameTime",
    "CPUBusy",
    "CPUWait",
    "GPULatency",
    "GPUTime",
    "GPUBusy",
    "GPUWait",
    "DisplayLatency",
    "DisplayedTime",
    "AnimationError",
    "AnimationTime",
    "MsFlipDelay",
    "AllInputToPhotonLatency",
    "ClickToPhotonLatency",
    "EtwBufferFillPct",
    "EtwBuffersInUse",
    "EtwTotalBuffers",
    "EtwEventsLost",
    "EtwBuffersLost"
)
$expectedPresentMonHeader = $expectedPresentMonColumns -join ","
$expectedPresentRuntime = "DXGI"

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-Gate1DisplayedStutterThresholdAssessment {
    param(
        [Parameter(Mandatory = $true)][bool]$DisplayCoveragePassed,
        [Parameter(Mandatory = $true)][uint64]$NotDisplayedRows,
        [Parameter(Mandatory = $true)][double]$MeanDisplayFps,
        [Parameter(Mandatory = $true)][double]$P95Milliseconds,
        [Parameter(Mandatory = $true)][double]$OnePercentLowFps,
        [Parameter(Mandatory = $true)][uint64]$Over33Count,
        [Parameter(Mandatory = $true)][uint64]$MaximumConsecutiveOver16
    )

    $metricThresholdsPassed = (
        $MeanDisplayFps -ge 60.0 -and
        $P95Milliseconds -le 16.7 -and
        $OnePercentLowFps -ge 45.0 -and
        $Over33Count -eq 0 -and
        $MaximumConsecutiveOver16 -le 1
    )
    return [pscustomobject][ordered]@{
        DisplayCoveragePassed = $DisplayCoveragePassed
        MetricThresholdsPassed = $metricThresholdsPassed
        NotDisplayedRows = $NotDisplayedRows
        HasNoNotDisplayedRows = ($NotDisplayedRows -eq 0)
        NotDisplayedRowsAreDiagnostic = $DisplayCoveragePassed
        ThresholdsPassed = ($DisplayCoveragePassed -and $metricThresholdsPassed)
    }
}

function Invoke-Gate1DisplayedStutterThresholdSelfTest {
    $passWithNotDisplayedRow = Get-Gate1DisplayedStutterThresholdAssessment `
        -DisplayCoveragePassed $true `
        -NotDisplayedRows 1 `
        -MeanDisplayFps 120.0 `
        -P95Milliseconds 12.0 `
        -OnePercentLowFps 60.0 `
        -Over33Count 0 `
        -MaximumConsecutiveOver16 1
    Assert-Condition -Condition ($passWithNotDisplayedRow.ThresholdsPassed -and $passWithNotDisplayedRow.MetricThresholdsPassed) -Message "Displayed-stutter threshold self-test treated one not-displayed row as a stutter despite continuous residency and passing displayed-frame metrics."
    Assert-Condition -Condition (-not $passWithNotDisplayedRow.HasNoNotDisplayedRows -and $passWithNotDisplayedRow.NotDisplayedRowsAreDiagnostic) -Message "Displayed-stutter threshold self-test did not retain the not-displayed row as an explicit diagnostic."

    $coverageFailure = Get-Gate1DisplayedStutterThresholdAssessment `
        -DisplayCoveragePassed $false `
        -NotDisplayedRows 1 `
        -MeanDisplayFps 120.0 `
        -P95Milliseconds 12.0 `
        -OnePercentLowFps 60.0 `
        -Over33Count 0 `
        -MaximumConsecutiveOver16 1
    Assert-Condition -Condition (-not $coverageFailure.ThresholdsPassed -and -not $coverageFailure.NotDisplayedRowsAreDiagnostic) -Message "Displayed-stutter threshold self-test accepted broken display-residency coverage."

    $metricFailures = @(
        @{ MeanDisplayFps = 59.9; P95Milliseconds = 12.0; OnePercentLowFps = 60.0; Over33Count = 0; MaximumConsecutiveOver16 = 1 },
        @{ MeanDisplayFps = 120.0; P95Milliseconds = 16.8; OnePercentLowFps = 60.0; Over33Count = 0; MaximumConsecutiveOver16 = 1 },
        @{ MeanDisplayFps = 120.0; P95Milliseconds = 12.0; OnePercentLowFps = 44.9; Over33Count = 0; MaximumConsecutiveOver16 = 1 },
        @{ MeanDisplayFps = 120.0; P95Milliseconds = 12.0; OnePercentLowFps = 60.0; Over33Count = 1; MaximumConsecutiveOver16 = 1 },
        @{ MeanDisplayFps = 120.0; P95Milliseconds = 12.0; OnePercentLowFps = 60.0; Over33Count = 0; MaximumConsecutiveOver16 = 2 }
    )
    foreach ($failure in $metricFailures) {
        $assessment = Get-Gate1DisplayedStutterThresholdAssessment `
            -DisplayCoveragePassed $true `
            -NotDisplayedRows 1 `
            -MeanDisplayFps $failure.MeanDisplayFps `
            -P95Milliseconds $failure.P95Milliseconds `
            -OnePercentLowFps $failure.OnePercentLowFps `
            -Over33Count $failure.Over33Count `
            -MaximumConsecutiveOver16 $failure.MaximumConsecutiveOver16
        Assert-Condition -Condition (-not $assessment.ThresholdsPassed -and -not $assessment.MetricThresholdsPassed -and $assessment.NotDisplayedRowsAreDiagnostic) -Message "Displayed-stutter threshold self-test accepted a failing displayed-frame metric."
    }
}

function Get-Gate1PresentationRouteContract {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Route)

    switch ($Route) {
        "AutoNoVsync" {
            return [pscustomobject]@{
                Name = "AutoNoVsync"
                RuntimeArguments = @("--no-vsync")
                RequestedPresentMode = "auto_no_vsync"
                PresentationRoute = "auto_no_vsync"
                VsyncRequested = $false
                QualificationEligible = $true
                ExpectedSyncInterval = "0"
                MarkerRequired = $false
            }
        }
        "FifoDiagnostic" {
            return [pscustomobject]@{
                Name = "FifoDiagnostic"
                RuntimeArguments = @("--gate1-fifo-diagnostic")
                RequestedPresentMode = "fifo"
                PresentationRoute = "fifo_diagnostic"
                VsyncRequested = $true
                QualificationEligible = $false
                ExpectedSyncInterval = $null
                MarkerRequired = $true
            }
        }
        default {
            throw "Unsupported Gate 1 presentation route '$Route'."
        }
    }
}

function Invoke-Gate1PresentationRouteSelfTest {
    $auto = Get-Gate1PresentationRouteContract -Route "AutoNoVsync"
    Assert-Condition -Condition ($auto.RuntimeArguments.Count -eq 1 -and $auto.RuntimeArguments[0] -eq "--no-vsync") -Message "Presentation-route self-test built the wrong AutoNoVsync arguments."
    Assert-Condition -Condition ($auto.RequestedPresentMode -eq "auto_no_vsync" -and $auto.PresentationRoute -eq "auto_no_vsync" -and $auto.VsyncRequested -eq $false -and $auto.QualificationEligible -eq $true -and $auto.ExpectedSyncInterval -eq "0" -and $auto.MarkerRequired -eq $false) -Message "Presentation-route self-test built the wrong AutoNoVsync contract."

    $fifo = Get-Gate1PresentationRouteContract -Route "FifoDiagnostic"
    Assert-Condition -Condition ($fifo.RuntimeArguments.Count -eq 1 -and $fifo.RuntimeArguments[0] -eq "--gate1-fifo-diagnostic" -and $fifo.RuntimeArguments -notcontains "--no-vsync") -Message "Presentation-route self-test built the wrong FifoDiagnostic arguments."
    Assert-Condition -Condition ($fifo.RequestedPresentMode -eq "fifo" -and $fifo.PresentationRoute -eq "fifo_diagnostic" -and $fifo.VsyncRequested -eq $true -and $fifo.QualificationEligible -eq $false -and $null -eq $fifo.ExpectedSyncInterval -and $fifo.MarkerRequired -eq $true) -Message "Presentation-route self-test built the wrong FifoDiagnostic contract."

    foreach ($invalidRoute in @("", "Fifo", "AutoVsync")) {
        $rejected = $false
        try {
            [void](Get-Gate1PresentationRouteContract -Route $invalidRoute)
        }
        catch {
            $rejected = $true
            Assert-Condition -Condition ($_.Exception.Message -like "*Unsupported Gate 1 presentation route*") -Message "Presentation-route negative self-test failed for an unexpected reason."
        }
        Assert-Condition -Condition $rejected -Message "Presentation-route negative self-test accepted '$invalidRoute'."
    }
}

function Get-FullPathWithoutResolving {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $relative = [IO.Path]::GetRelativePath($Root, $Candidate)
    return -not [IO.Path]::IsPathRooted($relative) -and
        $relative -ne ".." -and
        -not $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -and
        -not $relative.StartsWith("../", [StringComparison]::Ordinal)
}

function Assert-NoReparsePointInExistingAncestors {
    param([Parameter(Mandatory = $true)][string]$Path)

    $cursor = if ([IO.Directory]::Exists($Path)) { $Path } else { [IO.Path]::GetDirectoryName($Path) }
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if ([IO.Directory]::Exists($cursor)) {
            $item = Get-Item -LiteralPath $cursor -Force
            Assert-Condition -Condition (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "OutputRoot ancestor is a reparse point and cannot establish external evidence: $cursor"
        }
        $trimmed = $cursor.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        $parent = [IO.Path]::GetDirectoryName($trimmed)
        if ([string]::IsNullOrWhiteSpace($parent) -or [string]::Equals($parent, $cursor, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $cursor = $parent
    }
}

if (-not ("ForgeGate1Stutter.NativeWindow" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace ForgeGate1Stutter {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class NativeWindow {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
'@
}

if (-not ("ForgeGate1Stutter.ForegroundActivationV2" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace ForgeGate1Stutter {
    public static class ForegroundActivationV2 {
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        public static bool Activate(IntPtr hWnd, uint expectedProcessId) {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd) || IsIconic(hWnd)) {
                return false;
            }

            uint targetProcessId;
            uint targetThreadId = GetWindowThreadProcessId(hWnd, out targetProcessId);
            if (targetThreadId == 0 || targetProcessId != expectedProcessId) {
                return false;
            }
            if (GetForegroundWindow() == hWnd) {
                return true;
            }

            uint currentThreadId = GetCurrentThreadId();
            IntPtr initialForegroundWindow = GetForegroundWindow();
            uint foregroundProcessId;
            uint foregroundThreadId = initialForegroundWindow == IntPtr.Zero ? 0
                : GetWindowThreadProcessId(initialForegroundWindow, out foregroundProcessId);
            bool attachedToForeground = false;
            bool attachedToTarget = false;
            bool requestAccepted = false;
            try {
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId) {
                    attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }
                if (targetThreadId != currentThreadId && targetThreadId != foregroundThreadId) {
                    attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
                }
                BringWindowToTop(hWnd);
                requestAccepted = SetForegroundWindow(hWnd);
                SetActiveWindow(hWnd);
                SetFocus(hWnd);
                return requestAccepted || GetForegroundWindow() == hWnd;
            }
            finally {
                if (attachedToTarget) {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }
                if (attachedToForeground) {
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
        }
    }
}
'@
}

function Wait-ForQualifiedNativeWindow {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 5,
        [int]$MinimumWidth = 1920,
        [int]$MinimumHeight = 1080
    )

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $lastObservation = "HWND absent"
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process.HasExited) {
            throw "forge_runtime exited before native-window observation with code $($Process.ExitCode)."
        }

        $Process.Refresh()
        $windowHandle = $Process.MainWindowHandle
        if ($windowHandle -ne [IntPtr]::Zero) {
            [uint32]$ownerProcessId = 0
            $windowThreadId = [ForgeGate1Stutter.NativeWindow]::GetWindowThreadProcessId($windowHandle, [ref]$ownerProcessId)
            $visible = [ForgeGate1Stutter.NativeWindow]::IsWindowVisible($windowHandle)
            $minimized = [ForgeGate1Stutter.NativeWindow]::IsIconic($windowHandle)
            $rectangle = [ForgeGate1Stutter.Rect]::new()
            $rectangleObserved = [ForgeGate1Stutter.NativeWindow]::GetWindowRect($windowHandle, [ref]$rectangle)
            $width = if ($rectangleObserved) { $rectangle.Right - $rectangle.Left } else { 0 }
            $height = if ($rectangleObserved) { $rectangle.Bottom - $rectangle.Top } else { 0 }
            $lastObservation = "hwnd=$windowHandle owner=$ownerProcessId thread=$windowThreadId visible=$visible minimized=$minimized rect_observed=$rectangleObserved size=${width}x${height}"

            if (
                $windowThreadId -ne 0 -and
                $ownerProcessId -eq [uint32]$Process.Id -and
                $visible -and
                -not $minimized -and
                $rectangleObserved -and
                $width -ge $MinimumWidth -and
                $height -ge $MinimumHeight
            ) {
                return [pscustomobject]@{
                    Observed = $true
                    RawHandle = $windowHandle
                    Handle = ("0x{0:x16}" -f $windowHandle.ToInt64())
                    OwnerProcessId = $ownerProcessId
                    Visible = $true
                    Minimized = $false
                    OuterWidthReportedPx = $width
                    OuterHeightReportedPx = $height
                    ObservedAfterWaitStartMilliseconds = $timer.Elapsed.TotalMilliseconds
                }
            }
        }
        Start-Sleep -Milliseconds 100
    }

    throw "forge_runtime native window was not qualified within $TimeoutSeconds seconds; last observation: $lastObservation"
}

function Get-Gate1NativeWindowState {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][IntPtr]$ExpectedHandle
    )

    [uint64]$observedQpc = [Diagnostics.Stopwatch]::GetTimestamp()
    $Process.Refresh()
    $observedHandle = $Process.MainWindowHandle
    [uint32]$ownerProcessId = 0
    $windowThreadId = if ($observedHandle -ne [IntPtr]::Zero) {
        [ForgeGate1Stutter.NativeWindow]::GetWindowThreadProcessId($observedHandle, [ref]$ownerProcessId)
    }
    else {
        0
    }
    $visible = $observedHandle -ne [IntPtr]::Zero -and [ForgeGate1Stutter.NativeWindow]::IsWindowVisible($observedHandle)
    $minimized = $observedHandle -eq [IntPtr]::Zero -or [ForgeGate1Stutter.NativeWindow]::IsIconic($observedHandle)
    $foregroundHandle = [ForgeGate1Stutter.NativeWindow]::GetForegroundWindow()
    return [pscustomobject]@{
        Qpc = $observedQpc
        Handle = ("0x{0:x16}" -f $observedHandle.ToInt64())
        HandleMatches = ($observedHandle -eq $ExpectedHandle)
        OwnerProcessId = $ownerProcessId
        OwnerMatches = ($windowThreadId -ne 0 -and $ownerProcessId -eq [uint32]$Process.Id)
        Visible = $visible
        Minimized = $minimized
        ForegroundHandle = ("0x{0:x16}" -f $foregroundHandle.ToInt64())
        Foreground = ($foregroundHandle -eq $ExpectedHandle)
    }
}

function Invoke-Gate1RuntimeWindowForegroundAttempt {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][IntPtr]$ExpectedHandle,
        [int]$TimeoutMilliseconds = 2000
    )

    $timer = [Diagnostics.Stopwatch]::StartNew()
    Assert-Condition -Condition (-not $Process.HasExited) -Message "forge_runtime exited before foreground activation."
    $lastState = Get-Gate1NativeWindowState -Process $Process -ExpectedHandle $ExpectedHandle
    Assert-Condition -Condition ($lastState.HandleMatches -and $lastState.OwnerMatches -and $lastState.Visible -and -not $lastState.Minimized) -Message "The qualified forge_runtime HWND changed before foreground activation."
    $initialForeground = $lastState.Foreground
    $appActivateAttempted = $false
    $appActivateReturned = $false
    $appActivateError = $null
    $setForegroundWindowAttempted = $false
    $requestAccepted = $false
    $forcedActivationAttempted = $false
    $forcedActivationReturned = $false

    if (-not $lastState.Foreground) {
        $appActivateAttempted = $true
        $shell = $null
        try {
            $shell = New-Object -ComObject WScript.Shell
            $appActivateReturned = [bool]$shell.AppActivate($Process.Id)
        }
        catch {
            $appActivateError = $_.Exception.Message
        }
        finally {
            if ($null -ne $shell) {
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
            }
        }
        Start-Sleep -Milliseconds 100
        $lastState = Get-Gate1NativeWindowState -Process $Process -ExpectedHandle $ExpectedHandle
    }

    if (-not $lastState.Foreground) {
        Assert-Condition -Condition ($lastState.HandleMatches -and $lastState.OwnerMatches -and $lastState.Visible -and -not $lastState.Minimized) -Message "The qualified forge_runtime HWND changed before SetForegroundWindow."
        $setForegroundWindowAttempted = $true
        $requestAccepted = [ForgeGate1Stutter.NativeWindow]::SetForegroundWindow($ExpectedHandle)
        Start-Sleep -Milliseconds 100
        $lastState = Get-Gate1NativeWindowState -Process $Process -ExpectedHandle $ExpectedHandle
    }

    if (-not $lastState.Foreground) {
        Assert-Condition -Condition ($lastState.HandleMatches -and $lastState.OwnerMatches -and $lastState.Visible -and -not $lastState.Minimized) -Message "The qualified forge_runtime HWND changed before the attached-thread foreground fallback."
        $forcedActivationAttempted = $true
        $forcedActivationReturned = [ForgeGate1Stutter.ForegroundActivationV2]::Activate($ExpectedHandle, [uint32]$Process.Id)
    }

    do {
        if ($Process.HasExited) {
            break
        }
        $lastState = Get-Gate1NativeWindowState -Process $Process -ExpectedHandle $ExpectedHandle
        if ($lastState.Foreground) {
            break
        }
        Start-Sleep -Milliseconds 50
    } while ($timer.ElapsedMilliseconds -lt $TimeoutMilliseconds)

    if ($null -ne $lastState) {
        Assert-Condition -Condition ($lastState.HandleMatches -and $lastState.OwnerMatches -and $lastState.Visible -and -not $lastState.Minimized) -Message "The qualified forge_runtime HWND changed during foreground activation."
    }
    return [pscustomobject]@{
        InitialForeground = $initialForeground
        AppActivateAttempted = $appActivateAttempted
        AppActivateReturned = $appActivateReturned
        AppActivateError = $appActivateError
        SetForegroundWindowAttempted = $setForegroundWindowAttempted
        SetForegroundWindowReturned = $requestAccepted
        ForcedActivationAttempted = $forcedActivationAttempted
        ForcedActivationReturned = $forcedActivationReturned
        ForegroundObserved = ($null -ne $lastState -and $lastState.Foreground)
        ElapsedMilliseconds = $timer.Elapsed.TotalMilliseconds
        LastState = $lastState
    }
}

function Get-Gate1NativeWindowContinuity {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Samples,
        [Parameter(Mandatory = $true)][uint64]$StartQpc,
        [Parameter(Mandatory = $true)][uint64]$EndQpc,
        [Parameter(Mandatory = $true)][uint64]$FrequencyHz
    )

    Assert-Condition -Condition ($FrequencyHz -gt 0) -Message "Native-window continuity QPC frequency is invalid."
    $orderedSamples = @($Samples | Sort-Object { [uint64]$_.Qpc })
    $predecessor = @($orderedSamples | Where-Object { [uint64]$_.Qpc -le $StartQpc } | Select-Object -Last 1)
    $inside = @($orderedSamples | Where-Object { [uint64]$_.Qpc -gt $StartQpc -and [uint64]$_.Qpc -lt $EndQpc })
    $successor = @($orderedSamples | Where-Object { [uint64]$_.Qpc -ge $EndQpc } | Select-Object -First 1)
    $windowSamples = @($predecessor + $inside + $successor)
    $coversStart = $predecessor.Count -eq 1
    $coversEnd = $successor.Count -eq 1
    $handleMismatchSamples = @($windowSamples | Where-Object { -not $_.HandleMatches })
    $ownerMismatchSamples = @($windowSamples | Where-Object { -not $_.OwnerMatches })
    $notVisibleSamples = @($windowSamples | Where-Object { -not $_.Visible })
    $minimizedSamples = @($windowSamples | Where-Object { $_.Minimized })
    $notForegroundSamples = @($windowSamples | Where-Object { -not $_.Foreground })
    [uint64]$maximumSamplingGapQpc = 0
    for ($index = 1; $index -lt $windowSamples.Count; $index++) {
        [uint64]$gapQpc = [uint64]$windowSamples[$index].Qpc - [uint64]$windowSamples[$index - 1].Qpc
        if ($gapQpc -gt $maximumSamplingGapQpc) {
            $maximumSamplingGapQpc = $gapQpc
        }
    }
    $maximumSamplingGapMilliseconds = [double](([decimal]$maximumSamplingGapQpc * 1000) / [decimal]$FrequencyHz)
    $samplingGapPassed = $windowSamples.Count -gt 2 -and $maximumSamplingGapMilliseconds -le 250.0
    $statePassed = $coversStart -and $coversEnd -and $samplingGapPassed -and $handleMismatchSamples.Count -eq 0 -and $ownerMismatchSamples.Count -eq 0 -and $notVisibleSamples.Count -eq 0 -and $minimizedSamples.Count -eq 0 -and $notForegroundSamples.Count -eq 0
    return [pscustomobject]@{
        SamplingIntervalMilliseconds = 100
        TotalSamples = $orderedSamples.Count
        ControllerEnvelopeSamples = $windowSamples.Count
        CoversControllerStart = $coversStart
        CoversControllerEnd = $coversEnd
        FirstEnvelopeSampleQpc = if ($windowSamples.Count -gt 0) { [uint64]$windowSamples[0].Qpc } else { $null }
        LastEnvelopeSampleQpc = if ($windowSamples.Count -gt 0) { [uint64]$windowSamples[-1].Qpc } else { $null }
        MaximumSamplingGapQpc = $maximumSamplingGapQpc
        MaximumSamplingGapMilliseconds = $maximumSamplingGapMilliseconds
        MaximumSamplingGapThresholdMilliseconds = 250.0
        SamplingGapPassed = $samplingGapPassed
        HandleMismatchSamples = $handleMismatchSamples.Count
        OwnerMismatchSamples = $ownerMismatchSamples.Count
        NotVisibleSamples = $notVisibleSamples.Count
        MinimizedSamples = $minimizedSamples.Count
        NotForegroundSamples = $notForegroundSamples.Count
        FirstHandleMismatchQpc = if ($handleMismatchSamples.Count -gt 0) { [uint64]$handleMismatchSamples[0].Qpc } else { $null }
        FirstOwnerMismatchQpc = if ($ownerMismatchSamples.Count -gt 0) { [uint64]$ownerMismatchSamples[0].Qpc } else { $null }
        FirstNotVisibleQpc = if ($notVisibleSamples.Count -gt 0) { [uint64]$notVisibleSamples[0].Qpc } else { $null }
        FirstMinimizedQpc = if ($minimizedSamples.Count -gt 0) { [uint64]$minimizedSamples[0].Qpc } else { $null }
        FirstNotForegroundQpc = if ($notForegroundSamples.Count -gt 0) { [uint64]$notForegroundSamples[0].Qpc } else { $null }
        HandleStable = ($handleMismatchSamples.Count -eq 0)
        OwnerStable = ($ownerMismatchSamples.Count -eq 0)
        VisibleForAllSamples = ($notVisibleSamples.Count -eq 0)
        NeverMinimized = ($minimizedSamples.Count -eq 0)
        ForegroundForAllSamples = ($notForegroundSamples.Count -eq 0)
        StatePassed = $statePassed
    }
}

function Invoke-Gate1NativeWindowContinuitySelfTest {
    $goodSamples = @(
        [pscustomobject]@{ Qpc = [uint64]900; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true },
        [pscustomobject]@{ Qpc = [uint64]1050; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true },
        [pscustomobject]@{ Qpc = [uint64]1150; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true }
    )
    $good = Get-Gate1NativeWindowContinuity -Samples $goodSamples -StartQpc 1000 -EndQpc 1100 -FrequencyHz 1000
    Assert-Condition -Condition ($good.StatePassed -and $good.CoversControllerStart -and $good.CoversControllerEnd -and $good.MaximumSamplingGapMilliseconds -eq 150.0) -Message "Native-window continuity self-test rejected a covered 150 ms sample envelope."

    $gapSamples = @(
        [pscustomobject]@{ Qpc = [uint64]900; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true },
        [pscustomobject]@{ Qpc = [uint64]1050; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true },
        [pscustomobject]@{ Qpc = [uint64]1400; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true }
    )
    $gap = Get-Gate1NativeWindowContinuity -Samples $gapSamples -StartQpc 1000 -EndQpc 1100 -FrequencyHz 1000
    Assert-Condition -Condition (-not $gap.StatePassed -and -not $gap.SamplingGapPassed -and $gap.MaximumSamplingGapMilliseconds -eq 350.0) -Message "Native-window continuity self-test accepted a 350 ms sampling gap."

    $backgroundSamples = @(
        [pscustomobject]@{ Qpc = [uint64]900; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true },
        [pscustomobject]@{ Qpc = [uint64]1050; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $false },
        [pscustomobject]@{ Qpc = [uint64]1150; HandleMatches = $true; OwnerMatches = $true; Visible = $true; Minimized = $false; Foreground = $true }
    )
    $background = Get-Gate1NativeWindowContinuity -Samples $backgroundSamples -StartQpc 1000 -EndQpc 1100 -FrequencyHz 1000
    Assert-Condition -Condition (-not $background.StatePassed -and $background.NotForegroundSamples -eq 1) -Message "Native-window continuity self-test accepted a background sample."
}

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Assert-Condition -Condition ($null -ne $InputObject) -Message "$Path is absent."
    $matches = @($InputObject.PSObject.Properties | Where-Object {
            [string]::Equals($_.Name, $Name, [StringComparison]::Ordinal)
        })
    Assert-Condition -Condition ($matches.Count -eq 1) -Message "$Path.$Name is absent or duplicated."
    Assert-Condition -Condition ($null -ne $matches[0].Value) -Message "$Path.$Name is null."
    return $matches[0].Value
}

function Assert-QualifyingInstrumentationContract {
    param(
        [Parameter(Mandatory = $true)][object]$Instrumentation,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $renderDiagnosticsPluginEnabled = Get-RequiredProperty -InputObject $Instrumentation -Name "bevy_render_diagnostics_plugin_enabled" -Path $Path
    Assert-Condition -Condition ($renderDiagnosticsPluginEnabled -is [bool] -and $renderDiagnosticsPluginEnabled -eq $false) -Message "$Path enables Bevy RenderDiagnosticsPlugin or reports a non-Boolean value."
    $renderDiagnosticsResolveEncoderEnabled = Get-RequiredProperty -InputObject $Instrumentation -Name "bevy_render_diagnostics_resolve_encoder_enabled" -Path $Path
    Assert-Condition -Condition ($renderDiagnosticsResolveEncoderEnabled -is [bool] -and $renderDiagnosticsResolveEncoderEnabled -eq $false) -Message "$Path enables the Bevy render-diagnostics resolve encoder or reports a non-Boolean value."
    foreach ($field in @(
            "bevy_render_pass_cpu_timing_status",
            "bevy_render_pass_gpu_timing_status",
            "bevy_render_pipeline_statistics_status",
            "runtime_presented_frame_count_status"
        )) {
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $Instrumentation -Name $field -Path $Path) -eq "not_observed") -Message "$Path.$field must remain not_observed."
    }
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $Instrumentation -Name "application_update_timing_status" -Path $Path) -eq "observed_non_presentation") -Message "$Path mislabels application-update timing."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $Instrumentation -Name "display_timing_source" -Path $Path) -eq "external_presentmon_displayed_time") -Message "$Path does not reserve display timing for external PresentMon DisplayedTime."
}

function Test-FiniteNumber {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }
    try {
        return [double]::IsFinite([double]$Value)
    }
    catch {
        return $false
    }
}

function ConvertTo-InvariantDouble {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Field,
        [double]$Minimum = [double]::NegativeInfinity,
        [switch]$StrictlyGreaterThanMinimum
    )

    $parsed = 0.0
    $valid = [double]::TryParse(
        $Value,
        [Globalization.NumberStyles]::Float,
        [Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed
    )
    Assert-Condition -Condition ($valid -and [double]::IsFinite($parsed)) -Message "$Field is not a finite invariant number: '$Value'."
    if ($StrictlyGreaterThanMinimum) {
        Assert-Condition -Condition ($parsed -gt $Minimum) -Message "$Field must be greater than $Minimum, got $parsed."
    }
    else {
        Assert-Condition -Condition ($parsed -ge $Minimum) -Message "$Field must be at least $Minimum, got $parsed."
    }
    return $parsed
}

function ConvertTo-PresentMonMetric {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Field,
        [switch]$AllowNa,
        [double]$Minimum = [double]::NegativeInfinity,
        [switch]$StrictlyGreaterThanMinimum
    )

    if ([string]::Equals($Value, "NA", [StringComparison]::Ordinal)) {
        Assert-Condition -Condition $AllowNa.IsPresent -Message "$Field may not be NA."
        return $null
    }
    return ConvertTo-InvariantDouble -Value $Value -Field $Field -Minimum $Minimum -StrictlyGreaterThanMinimum:$StrictlyGreaterThanMinimum
}

function Write-JsonAtomically {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    Assert-Condition -Condition (-not [IO.File]::Exists($Path)) -Message "Refusing to replace existing artifact $Path."
    $temporaryPath = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    try {
        $json = ($Value | ConvertTo-Json -Depth 64) + [Environment]::NewLine
        [IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $Path, $false)
    }
    finally {
        if ([IO.File]::Exists($temporaryPath)) {
            [IO.File]::Delete($temporaryPath)
        }
    }
}

function Get-FirstLineShared {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite
    )
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 4096, $true)
        try {
            return $reader.ReadLine()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-SharedText {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite
    )
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 4096, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-PresentMonConsoleLine {
    param([AllowEmptyString()][string]$Line)

    $trimmed = $Line.Trim()
    return (
        [string]::IsNullOrWhiteSpace($trimmed) -or
        $trimmed -match '^\[ETW Status\] '
    )
}

function Get-PresentMonStreamSnapshot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedHeader,
        [switch]$Final
    )

    if (-not [IO.File]::Exists($Path)) {
        return [pscustomobject]@{
            HeaderObserved = $false
            Rows = @()
            CsvLines = @()
            ConsoleLines = @()
            UnknownLines = @()
        }
    }
    $text = Get-SharedText -Path $Path
    if ([string]::IsNullOrEmpty($text)) {
        return [pscustomobject]@{
            HeaderObserved = $false
            Rows = @()
            CsvLines = @()
            ConsoleLines = @()
            UnknownLines = @()
        }
    }

    $lines = @([regex]::Split($text, '\r?\n'))
    $endsWithNewLine = $text.EndsWith("`n", [StringComparison]::Ordinal)
    if ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Length -eq 0) {
        $lines = if ($lines.Count -eq 1) { @() } else { @($lines[0..($lines.Count - 2)]) }
    }
    elseif (-not $Final -and -not $endsWithNewLine) {
        $lines = if ($lines.Count -eq 1) { @() } else { @($lines[0..($lines.Count - 2)]) }
    }

    $headerObserved = $false
    $expectedColumnCount = ([regex]::Split($ExpectedHeader, ',')).Count
    $csvLines = [Collections.Generic.List[string]]::new()
    $consoleLines = [Collections.Generic.List[string]]::new()
    $unknownLines = [Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        $normalized = $line.TrimStart([char]0xfeff)
        if ([string]::Equals($normalized, $ExpectedHeader, [StringComparison]::Ordinal)) {
            if ($headerObserved) {
                $unknownLines.Add("duplicate CSV header")
            }
            else {
                $headerObserved = $true
                $csvLines.Add($normalized)
            }
            continue
        }
        if (Test-PresentMonConsoleLine -Line $normalized) {
            if (-not [string]::IsNullOrWhiteSpace($normalized)) {
                $consoleLines.Add($normalized.Trim())
            }
            continue
        }
        if ($headerObserved) {
            $actualColumnCount = ([regex]::Split($normalized, ',')).Count
            if ($actualColumnCount -eq $expectedColumnCount) {
                $csvLines.Add($normalized)
            }
            else {
                $unknownLines.Add("CSV row has $actualColumnCount columns; expected $expectedColumnCount")
            }
        }
        else {
            $unknownLines.Add($normalized)
        }
    }

    $rows = @()
    if ($csvLines.Count -gt 1) {
        $rows = @($csvLines.ToArray() | ConvertFrom-Csv)
    }
    return [pscustomobject]@{
        HeaderObserved = $headerObserved
        Rows = @($rows)
        CsvLines = $csvLines.ToArray()
        ConsoleLines = $consoleLines.ToArray()
        UnknownLines = $unknownLines.ToArray()
    }
}

function Test-QualifiedPresentMonPreSignalRow {
    param(
        [Parameter(Mandatory = $true)][object]$Row,
        [Parameter(Mandatory = $true)][int]$ExpectedColumnCount,
        [Parameter(Mandatory = $true)][string]$ExpectedApplication,
        [Parameter(Mandatory = $true)][int]$ExpectedProcessId,
        [Parameter(Mandatory = $true)][string]$ExpectedRuntime
    )

    if (@($Row.PSObject.Properties).Count -ne $ExpectedColumnCount) {
        return $false
    }
    if (-not [string]::Equals([string]$Row.Application, $ExpectedApplication, [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $processId = 0
    if (-not [int]::TryParse([string]$Row.ProcessID, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$processId) -or $processId -ne $ExpectedProcessId) {
        return $false
    }
    if ([string]$Row.SwapChainAddress -notmatch '^0x[0-9A-Fa-f]+$' -or -not [string]::Equals([string]$Row.PresentRuntime, $ExpectedRuntime, [StringComparison]::Ordinal)) {
        return $false
    }
    if ([string]::IsNullOrWhiteSpace([string]$Row.PresentMode) -or [string]::Equals([string]$Row.PresentMode, "NA", [StringComparison]::Ordinal)) {
        return $false
    }

    [uint64]$qpc = 0
    if (-not [uint64]::TryParse([string]$Row.CPUStartQPC, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$qpc) -or $qpc -eq 0) {
        return $false
    }
    foreach ($field in @("FrameTime", "DisplayLatency", "DisplayedTime")) {
        [double]$metric = 0.0
        if (-not [double]::TryParse([string]$Row.$field, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$metric) -or -not [double]::IsFinite($metric) -or $metric -le 0.0) {
            return $false
        }
    }
    [double]$etwFill = 0.0
    if (-not [double]::TryParse(([string]$Row.EtwBufferFillPct -replace '%$', ''), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$etwFill) -or -not [double]::IsFinite($etwFill) -or $etwFill -lt 0.0 -or $etwFill -ge 100.0) {
        return $false
    }
    [uint64]$etwInUse = 0
    [uint64]$etwTotal = 0
    [uint64]$etwEventsLost = 0
    [uint64]$etwBuffersLost = 0
    if (
        -not [uint64]::TryParse([string]$Row.EtwBuffersInUse, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$etwInUse) -or
        -not [uint64]::TryParse([string]$Row.EtwTotalBuffers, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$etwTotal) -or
        -not [uint64]::TryParse([string]$Row.EtwEventsLost, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$etwEventsLost) -or
        -not [uint64]::TryParse([string]$Row.EtwBuffersLost, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$etwBuffersLost) -or
        $etwTotal -eq 0 -or
        $etwInUse -gt $etwTotal -or
        $etwEventsLost -ne 0 -or
        $etwBuffersLost -ne 0
    ) {
        return $false
    }
    return $true
}

function Invoke-PresentMonStreamParserSelfTest {
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedHeader,
        [Parameter(Mandatory = $true)][int]$ExpectedColumnCount,
        [Parameter(Mandatory = $true)][string]$ExpectedRuntime
    )

    $values = @(
        "forge_runtime.exe", "4242", "0xABC", $ExpectedRuntime, "0", "0", "0", "Composed: Flip",
        "123456", "16.0", "10.0", "6.0", "1.0", "1.0", "1.0", "0.0", "16.0", "16.0",
        "0.0", "16.0", "0.0", "NA", "NA", "1.0", "1", "256", "0", "0"
    )
    Assert-Condition -Condition ($values.Count -eq $ExpectedColumnCount) -Message "PresentMon parser self-test row has the wrong column count."
    $statusLine = "[ETW Status] BufferFillPct=1.0% BuffersInUse=1 TotalBuffers=256 EventsLost=0 BuffersLost=0"
    $temporaryPath = [IO.Path]::GetTempFileName()
    try {
        $row = $values -join ","
        [IO.File]::WriteAllText($temporaryPath, "$statusLine`r`n$ExpectedHeader`r`n$row", [Text.UTF8Encoding]::new($false))
        $liveSnapshot = Get-PresentMonStreamSnapshot -Path $temporaryPath -ExpectedHeader $ExpectedHeader
        Assert-Condition -Condition ($liveSnapshot.HeaderObserved -and $liveSnapshot.Rows.Count -eq 0 -and $liveSnapshot.UnknownLines.Count -eq 0) -Message "PresentMon parser self-test accepted an unterminated live row."
        $finalSnapshot = Get-PresentMonStreamSnapshot -Path $temporaryPath -ExpectedHeader $ExpectedHeader -Final
        Assert-Condition -Condition ($finalSnapshot.Rows.Count -eq 1 -and $finalSnapshot.ConsoleLines.Count -eq 1 -and $finalSnapshot.UnknownLines.Count -eq 0) -Message "PresentMon parser self-test did not recover the complete final row."
        Assert-Condition -Condition (Test-QualifiedPresentMonPreSignalRow -Row $finalSnapshot.Rows[0] -ExpectedColumnCount $ExpectedColumnCount -ExpectedApplication "forge_runtime.exe" -ExpectedProcessId 4242 -ExpectedRuntime $ExpectedRuntime) -Message "PresentMon parser self-test rejected its valid strict row."

        $naRow = $finalSnapshot.Rows[0] | Select-Object *
        $naRow.DisplayedTime = "NA"
        Assert-Condition -Condition (-not (Test-QualifiedPresentMonPreSignalRow -Row $naRow -ExpectedColumnCount $ExpectedColumnCount -ExpectedApplication "forge_runtime.exe" -ExpectedProcessId 4242 -ExpectedRuntime $ExpectedRuntime)) -Message "PresentMon parser self-test accepted a pre-signal NA display row."
        $lostRow = $finalSnapshot.Rows[0] | Select-Object *
        $lostRow.EtwBuffersLost = "1"
        Assert-Condition -Condition (-not (Test-QualifiedPresentMonPreSignalRow -Row $lostRow -ExpectedColumnCount $ExpectedColumnCount -ExpectedApplication "forge_runtime.exe" -ExpectedProcessId 4242 -ExpectedRuntime $ExpectedRuntime)) -Message "PresentMon parser self-test accepted an ETW-loss row."

        [IO.File]::WriteAllText($temporaryPath, "$ExpectedHeader`r`n$row,INJECTED`r`n", [Text.UTF8Encoding]::new($false))
        $extraColumnSnapshot = Get-PresentMonStreamSnapshot -Path $temporaryPath -ExpectedHeader $ExpectedHeader -Final
        Assert-Condition -Condition ($extraColumnSnapshot.HeaderObserved -and $extraColumnSnapshot.Rows.Count -eq 0 -and $extraColumnSnapshot.UnknownLines.Count -eq 1) -Message "PresentMon parser self-test accepted a row with a trailing extra column."

        $interleavedValues = [string[]]$values.Clone()
        $interleavedValues[17] = "16.0[ETW Status] BufferFillPct=1.0%"
        [IO.File]::WriteAllText($temporaryPath, "$ExpectedHeader`r`n$($interleavedValues -join ',')`r`n", [Text.UTF8Encoding]::new($false))
        $interleavedSnapshot = Get-PresentMonStreamSnapshot -Path $temporaryPath -ExpectedHeader $ExpectedHeader -Final
        Assert-Condition -Condition ($interleavedSnapshot.Rows.Count -eq 1 -and -not (Test-QualifiedPresentMonPreSignalRow -Row $interleavedSnapshot.Rows[0] -ExpectedColumnCount $ExpectedColumnCount -ExpectedApplication "forge_runtime.exe" -ExpectedProcessId 4242 -ExpectedRuntime $ExpectedRuntime)) -Message "PresentMon parser self-test did not fail closed on an interleaved row."
    }
    finally {
        if ([IO.File]::Exists($temporaryPath)) {
            [IO.File]::Delete($temporaryPath)
        }
    }
}

function Get-VerifiedRuntimeControllerQpcWindow {
    param(
        [Parameter(Mandatory = $true)][uint64]$HostLowerBoundQpc,
        [Parameter(Mandatory = $true)][uint64]$StartQpc,
        [Parameter(Mandatory = $true)][uint64]$EndQpc,
        [Parameter(Mandatory = $true)][uint64]$RuntimeFrequencyHz,
        [Parameter(Mandatory = $true)][uint64]$HostFrequencyHz
    )

    Assert-Condition -Condition ($StartQpc -gt $HostLowerBoundQpc) -Message "Runtime controller QPC start does not follow the host lower boundary."
    Assert-Condition -Condition ($EndQpc -gt $StartQpc) -Message "Runtime controller QPC bounds are absent or unordered."
    Assert-Condition -Condition ($RuntimeFrequencyHz -gt 0 -and $RuntimeFrequencyHz -eq $HostFrequencyHz) -Message "Runtime controller QPC frequency does not match the host clock."
    $durationSeconds = [double](([decimal]$EndQpc - [decimal]$StartQpc) / [decimal]$RuntimeFrequencyHz)
    Assert-Condition -Condition ([double]::IsFinite($durationSeconds) -and $durationSeconds -gt 0.0) -Message "Runtime controller QPC duration is invalid."
    return [pscustomobject]@{
        StartQpc = $StartQpc
        EndQpc = $EndQpc
        FrequencyHz = $RuntimeFrequencyHz
        DurationSeconds = $durationSeconds
    }
}

function Get-PresentMonControllerWindowProjection {
    param(
        [Parameter(Mandatory = $true)][object[]]$Rows,
        [Parameter(Mandatory = $true)][uint64]$StartQpc,
        [Parameter(Mandatory = $true)][uint64]$EndQpc,
        [Parameter(Mandatory = $true)][uint64]$FrequencyHz
    )

    Assert-Condition -Condition ($StartQpc -gt 0 -and $EndQpc -gt $StartQpc -and $FrequencyHz -gt 0) -Message "Runtime controller QPC projection inputs are invalid."
    [decimal]$start = $StartQpc
    [decimal]$end = $EndQpc
    [decimal]$frequency = $FrequencyHz
    # PresentMon prints DisplayLatency and DisplayedTime with four decimal places in milliseconds.
    # Two printed quanta cover the worst-case adjacent-boundary rounding after both terms are added.
    [decimal]$quantizationToleranceQpc = [Math]::Max(1, [Math]::Ceiling([decimal]$FrequencyHz / [decimal]5000000))
    $displayedRows = [Collections.Generic.List[object]]::new()
    $notDisplayedRows = [Collections.Generic.List[object]]::new()
    foreach ($row in $Rows) {
        [decimal]$cpuStartQpc = $row.Qpc
        if ($null -eq $row.DisplayedTimeMs) {
            if ($cpuStartQpc -ge $start -and $cpuStartQpc -lt $end) {
                $notDisplayedRows.Add($row)
            }
            continue
        }
        $displayStartQpc = $cpuStartQpc + ([decimal]([double]$row.DisplayLatencyMs) * $frequency / 1000)
        $displayEndQpc = $displayStartQpc + ([decimal]([double]$row.DisplayedTimeMs) * $frequency / 1000)
        if ($displayEndQpc -gt $start -and $displayStartQpc -lt $end) {
            $displayedRows.Add([pscustomobject]@{
                    CsvRow = $row.CsvRow
                    Qpc = $cpuStartQpc
                    DisplayLatencyMs = $row.DisplayLatencyMs
                    DisplayedTimeMs = $row.DisplayedTimeMs
                    DerivedDisplayStartQpc = $displayStartQpc
                    DerivedDisplayEndQpc = $displayEndQpc
            })
        }
    }
    $orderedDisplayedRows = @($displayedRows.ToArray() | Sort-Object { [decimal]$_.DerivedDisplayStartQpc })
    $coversStart = $false
    $coversEnd = $false
    [decimal]$maximumGapQpc = 0
    [decimal]$totalGapQpc = 0
    [decimal]$coverageCursor = $start
    foreach ($row in $orderedDisplayedRows) {
        [decimal]$displayStartQpc = $row.DerivedDisplayStartQpc
        [decimal]$displayEndQpc = $row.DerivedDisplayEndQpc
        if ($displayStartQpc -le ($start + $quantizationToleranceQpc) -and $displayEndQpc -gt $start) {
            $coversStart = $true
        }
        if ($displayStartQpc -lt $end -and $displayEndQpc -ge ($end - $quantizationToleranceQpc)) {
            $coversEnd = $true
        }
        [decimal]$clippedStartQpc = [Math]::Max([decimal]$row.DerivedDisplayStartQpc, $start)
        [decimal]$clippedEndQpc = [Math]::Min([decimal]$row.DerivedDisplayEndQpc, $end)
        if ($clippedStartQpc -gt $coverageCursor) {
            $gapQpc = $clippedStartQpc - $coverageCursor
            $maximumGapQpc = [Math]::Max($maximumGapQpc, $gapQpc)
            $totalGapQpc += $gapQpc
        }
        if ($clippedEndQpc -gt $coverageCursor) {
            $coverageCursor = $clippedEndQpc
        }
    }
    if ($coverageCursor -lt $end) {
        $gapQpc = $end - $coverageCursor
        $maximumGapQpc = [Math]::Max($maximumGapQpc, $gapQpc)
        $totalGapQpc += $gapQpc
    }
    return [pscustomobject]@{
        Interval = "half_open_start_inclusive_end_exclusive"
        DisplayTimingSource = "derived_from_cpu_start_qpc_plus_presentmon_display_latency_and_displayed_time"
        DisplayedOverlapRows = $orderedDisplayedRows
        NotDisplayedCpuStartRows = @($notDisplayedRows.ToArray())
        CoversStart = $coversStart
        CoversEnd = $coversEnd
        QuantizationToleranceQpc = $quantizationToleranceQpc
        MaximumGapQpc = $maximumGapQpc
        TotalGapQpc = $totalGapQpc
    }
}

function Assert-PresentMonControllerWindowCoverage {
    param([Parameter(Mandatory = $true)][object]$Projection)

    Assert-Condition -Condition ($Projection.CoversStart -eq $true) -Message "PresentMon-derived display residency does not cover the runtime controller start boundary."
    Assert-Condition -Condition ($Projection.CoversEnd -eq $true) -Message "PresentMon-derived display residency does not cover the runtime controller end boundary."
    Assert-Condition -Condition ([decimal]$Projection.MaximumGapQpc -le [decimal]$Projection.QuantizationToleranceQpc) -Message "PresentMon-derived display residency has a gap of $($Projection.MaximumGapQpc) QPC ticks; tolerance is $($Projection.QuantizationToleranceQpc)."
}

function Invoke-PresentMonControllerQpcWindowSelfTest {
    $rows = @(
        [pscustomobject]@{ CsvRow = 1; Qpc = [uint64]40; DisplayLatencyMs = 50.0; DisplayedTimeMs = 10.0 },
        [pscustomobject]@{ CsvRow = 2; Qpc = [uint64]50; DisplayLatencyMs = 40.0; DisplayedTimeMs = 70.0 },
        [pscustomobject]@{ CsvRow = 3; Qpc = [uint64]90; DisplayLatencyMs = 10.0; DisplayedTimeMs = 50.0 },
        [pscustomobject]@{ CsvRow = 4; Qpc = [uint64]150; DisplayLatencyMs = 10.0; DisplayedTimeMs = 40.0 },
        [pscustomobject]@{ CsvRow = 5; Qpc = [uint64]190; DisplayLatencyMs = 10.0; DisplayedTimeMs = 20.0 },
        [pscustomobject]@{ CsvRow = 6; Qpc = [uint64]100; DisplayLatencyMs = $null; DisplayedTimeMs = $null },
        [pscustomobject]@{ CsvRow = 7; Qpc = [uint64]200; DisplayLatencyMs = $null; DisplayedTimeMs = $null }
    )
    $projection = Get-PresentMonControllerWindowProjection -Rows $rows -StartQpc 100 -EndQpc 200 -FrequencyHz 1000
    $selected = @($projection.DisplayedOverlapRows)
    Assert-Condition -Condition ($selected.Count -eq 3) -Message "PresentMon projection self-test selected the wrong display-overlap count."
    Assert-Condition -Condition ([uint64]$selected[0].Qpc -eq 50) -Message "PresentMon projection self-test omitted the predecessor already displayed across runtime start."
    Assert-Condition -Condition (@($selected | Where-Object { [uint64]$_.Qpc -eq 90 }).Count -eq 1) -Message "PresentMon projection self-test excluded the inclusive display start boundary."
    Assert-Condition -Condition (@($selected | Where-Object { [uint64]$_.Qpc -eq 40 }).Count -eq 0) -Message "PresentMon projection self-test included a frame whose display interval ends at runtime start."
    Assert-Condition -Condition (@($selected | Where-Object { [uint64]$_.Qpc -eq 190 }).Count -eq 0) -Message "PresentMon projection self-test included a frame whose display interval begins at runtime end."
    Assert-Condition -Condition ($projection.CoversStart -and $projection.CoversEnd -and [decimal]$projection.MaximumGapQpc -eq 0) -Message "PresentMon projection self-test did not prove contiguous boundary coverage."
    Assert-Condition -Condition (@($projection.NotDisplayedCpuStartRows).Count -eq 1 -and [uint64]$projection.NotDisplayedCpuStartRows[0].Qpc -eq 100) -Message "PresentMon projection self-test did not apply a half-open CPU interval to not-displayed rows."
    Assert-PresentMonControllerWindowCoverage -Projection $projection
    $gappedRows = @($rows | ForEach-Object { $_ | Select-Object * })
    $gappedRows[3].DisplayLatencyMs = 13.0
    $gappedProjection = Get-PresentMonControllerWindowProjection -Rows $gappedRows -StartQpc 100 -EndQpc 200 -FrequencyHz 1000
    Assert-Condition -Condition ($gappedProjection.CoversStart -and $gappedProjection.CoversEnd -and [decimal]$gappedProjection.MaximumGapQpc -eq 3) -Message "PresentMon projection self-test did not expose an internal display gap above quantization tolerance."
    $gapRejected = $false
    try {
        Assert-PresentMonControllerWindowCoverage -Projection $gappedProjection
    }
    catch {
        $gapRejected = $true
        Assert-Condition -Condition ($_.Exception.Message -like "*has a gap of 3 QPC ticks*") -Message "PresentMon projection gap self-test failed for an unexpected reason."
    }
    Assert-Condition -Condition $gapRejected -Message "PresentMon projection gap self-test accepted a gap above quantization tolerance."
    $verified = Get-VerifiedRuntimeControllerQpcWindow -HostLowerBoundQpc 99 -StartQpc 100 -EndQpc 300 -RuntimeFrequencyHz 100 -HostFrequencyHz 100
    Assert-Condition -Condition ($verified.DurationSeconds -eq 2.0) -Message "Runtime QPC self-test computed the wrong duration."

    $negativeCases = @(
        @{ Arguments = @{ HostLowerBoundQpc = 100; StartQpc = 100; EndQpc = 300; RuntimeFrequencyHz = 100; HostFrequencyHz = 100 }; Expected = "does not follow the host lower boundary" },
        @{ Arguments = @{ HostLowerBoundQpc = 99; StartQpc = 100; EndQpc = 100; RuntimeFrequencyHz = 100; HostFrequencyHz = 100 }; Expected = "absent or unordered" },
        @{ Arguments = @{ HostLowerBoundQpc = 99; StartQpc = 100; EndQpc = 300; RuntimeFrequencyHz = 99; HostFrequencyHz = 100 }; Expected = "does not match the host clock" }
    )
    foreach ($case in $negativeCases) {
        $rejected = $false
        $arguments = $case.Arguments
        try {
            [void](Get-VerifiedRuntimeControllerQpcWindow @arguments)
        }
        catch {
            $rejected = $true
            Assert-Condition -Condition ($_.Exception.Message -like "*$($case.Expected)*") -Message "Runtime QPC negative self-test failed for an unexpected reason."
        }
        Assert-Condition -Condition $rejected -Message "Runtime QPC negative self-test accepted an invalid boundary contract."
    }
}

function Write-PresentMonStreamArtifacts {
    param(
        [Parameter(Mandatory = $true)][string]$RawPath,
        [Parameter(Mandatory = $true)][string]$CsvPath,
        [Parameter(Mandatory = $true)][string]$ConsolePath,
        [Parameter(Mandatory = $true)][string]$ExpectedHeader
    )

    $snapshot = Get-PresentMonStreamSnapshot -Path $RawPath -ExpectedHeader $ExpectedHeader -Final
    Assert-Condition -Condition $snapshot.HeaderObserved -Message "The completed PresentMon stdout stream has no exact governed CSV header."
    Assert-Condition -Condition ($snapshot.UnknownLines.Count -eq 0) -Message "The completed PresentMon stdout stream contains unknown or malformed records: $($snapshot.UnknownLines -join '; ')."
    Assert-Condition -Condition ($snapshot.Rows.Count -gt 0) -Message "The completed PresentMon stdout stream contains no CSV rows."
    [IO.File]::WriteAllLines($CsvPath, [string[]]$snapshot.CsvLines, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllLines($ConsolePath, [string[]]$snapshot.ConsoleLines, [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{
        CsvRows = $snapshot.Rows.Count
        CsvLines = $snapshot.CsvLines.Count
        ConsoleLines = $snapshot.ConsoleLines.Count
    }
}

function Start-CapturedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath,
        [Parameter(Mandatory = $true)][bool]$Visible,
        [hashtable]$EnvironmentVariables = @{},
        [switch]$StreamToFiles
    )

    $process = $null
    $stdoutStream = $null
    $stderrStream = $null
    try {
        if ($StreamToFiles) {
            Assert-Condition -Condition (-not [IO.File]::Exists($StdoutPath)) -Message "$Label stdout path already exists."
            Assert-Condition -Condition (-not [IO.File]::Exists($StderrPath)) -Message "$Label stderr path already exists."
        }
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $FilePath
        $startInfo.WorkingDirectory = $WorkingDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = -not $Visible
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        if (-not $Visible) {
            $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        }
        foreach ($argument in $ArgumentList) {
            [void]$startInfo.ArgumentList.Add($argument)
        }
        foreach ($name in $EnvironmentVariables.Keys) {
            $startInfo.Environment[[string]$name] = [string]$EnvironmentVariables[$name]
        }

        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        Assert-Condition -Condition ($process.Start()) -Message "Cannot start $Label."
        if ($StreamToFiles) {
            $stdoutStream = [IO.FileStream]::new(
                $StdoutPath,
                [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write,
                [IO.FileShare]::Read,
                1,
                [IO.FileOptions]::Asynchronous
            )
            $stderrStream = [IO.FileStream]::new(
                $StderrPath,
                [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write,
                [IO.FileShare]::Read,
                1,
                [IO.FileOptions]::Asynchronous
            )
            $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdoutStream)
            $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderrStream)
        }
        else {
            $stdoutTask = $process.StandardOutput.ReadToEndAsync()
            $stderrTask = $process.StandardError.ReadToEndAsync()
        }
        return [pscustomobject]@{
            Label = $Label
            Process = $process
            StdoutTask = $stdoutTask
            StderrTask = $stderrTask
            StdoutPath = $StdoutPath
            StderrPath = $StderrPath
            StreamToFiles = [bool]$StreamToFiles
            StdoutStream = $stdoutStream
            StderrStream = $stderrStream
            OutputSaved = $false
        }
    }
    catch {
        if ($null -ne $process) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill($true)
                    [void]$process.WaitForExit(5000)
                }
            }
            catch {
                # The outer watchdog cannot retain a capture object when startup itself failed.
            }
            $process.Dispose()
        }
        if ($null -ne $stdoutStream) {
            $stdoutStream.Dispose()
        }
        if ($null -ne $stderrStream) {
            $stderrStream.Dispose()
        }
        throw
    }
}

function Stop-CapturedProcessBounded {
    param([AllowNull()][object]$Capture)

    if ($null -eq $Capture) {
        return $true
    }
    try {
        if ($Capture.Process.HasExited) {
            return $true
        }
        $Capture.Process.Kill($true)
        return $Capture.Process.WaitForExit(5000)
    }
    catch {
        return $false
    }
}

function Save-CapturedProcessOutput {
    param(
        [Parameter(Mandatory = $true)][object]$Capture,
        [Parameter(Mandatory = $true)][int]$DrainTimeoutMilliseconds
    )

    if ($Capture.OutputSaved) {
        return [pscustomobject]@{ StdoutDrained = $true; StderrDrained = $true }
    }

    if ($Capture.StreamToFiles) {
        $stdoutDrained = $false
        $stderrDrained = $false
        try {
            $stdoutDrained = $Capture.StdoutTask.Wait($DrainTimeoutMilliseconds)
            if ($stdoutDrained) {
                [void]$Capture.StdoutTask.GetAwaiter().GetResult()
            }
        }
        catch {
            $stdoutDrained = $false
        }
        finally {
            try {
                $Capture.StdoutStream.Flush()
            }
            catch {
            }
            $Capture.StdoutStream.Dispose()
        }
        try {
            $stderrDrained = $Capture.StderrTask.Wait($DrainTimeoutMilliseconds)
            if ($stderrDrained) {
                [void]$Capture.StderrTask.GetAwaiter().GetResult()
            }
        }
        catch {
            $stderrDrained = $false
        }
        finally {
            try {
                $Capture.StderrStream.Flush()
            }
            catch {
            }
            $Capture.StderrStream.Dispose()
        }
        $Capture.OutputSaved = $true
        return [pscustomobject]@{
            StdoutDrained = $stdoutDrained
            StderrDrained = $stderrDrained
        }
    }

    $stdout = ""
    $stderr = ""
    $stdoutDrained = $false
    $stderrDrained = $false
    try {
        $stdoutDrained = $Capture.StdoutTask.Wait($DrainTimeoutMilliseconds)
        if ($stdoutDrained) {
            $stdout = $Capture.StdoutTask.GetAwaiter().GetResult()
        }
    }
    catch {
        $stdout = "[$($Capture.Label) watchdog] stdout drainage failed: $($_.Exception.Message)"
    }
    try {
        $stderrDrained = $Capture.StderrTask.Wait($DrainTimeoutMilliseconds)
        if ($stderrDrained) {
            $stderr = $Capture.StderrTask.GetAwaiter().GetResult()
        }
    }
    catch {
        $stderr = "[$($Capture.Label) watchdog] stderr drainage failed: $($_.Exception.Message)"
    }
    if (-not $stdoutDrained -and [string]::IsNullOrWhiteSpace($stdout)) {
        $stdout = "[$($Capture.Label) watchdog] stdout did not drain within $DrainTimeoutMilliseconds ms."
    }
    if (-not $stderrDrained -and [string]::IsNullOrWhiteSpace($stderr)) {
        $stderr = "[$($Capture.Label) watchdog] stderr did not drain within $DrainTimeoutMilliseconds ms."
    }
    [IO.File]::WriteAllText($Capture.StdoutPath, $stdout, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($Capture.StderrPath, $stderr, [Text.UTF8Encoding]::new($false))
    $Capture.OutputSaved = $true
    return [pscustomobject]@{
        StdoutDrained = $stdoutDrained
        StderrDrained = $stderrDrained
    }
}

function Invoke-Gate1CapturedProcessOutputSelfTest {
    $stdoutSource = [IO.MemoryStream]::new([byte[]](1, 2, 3))
    $stderrSource = [IO.MemoryStream]::new([byte[]](4, 5))
    $stdoutDestination = [IO.MemoryStream]::new()
    $stderrDestination = [IO.MemoryStream]::new()
    $capture = [pscustomobject]@{
        Label = "captured-output-self-test"
        StdoutTask = $stdoutSource.CopyToAsync($stdoutDestination)
        StderrTask = $stderrSource.CopyToAsync($stderrDestination)
        StdoutPath = "unused.stdout.log"
        StderrPath = "unused.stderr.log"
        StreamToFiles = $true
        StdoutStream = $stdoutDestination
        StderrStream = $stderrDestination
        OutputSaved = $false
    }
    try {
        $result = @(Save-CapturedProcessOutput -Capture $capture -DrainTimeoutMilliseconds 1000)
        Assert-Condition -Condition ($result.Count -eq 1) -Message "Captured-process output leaked task results into the pipeline."
        Assert-Condition -Condition ($result[0].StdoutDrained -and $result[0].StderrDrained) -Message "Captured-process output self-test did not drain both streams."
        $secondResult = @(Save-CapturedProcessOutput -Capture $capture -DrainTimeoutMilliseconds 1000)
        Assert-Condition -Condition ($secondResult.Count -eq 1 -and $secondResult[0].StdoutDrained -and $secondResult[0].StderrDrained) -Message "Captured-process output self-test is not idempotent."
    }
    finally {
        $stdoutSource.Dispose()
        $stderrSource.Dispose()
    }
}

function Invoke-CapturedProcessBounded {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$Label,
        [AllowNull()][AllowEmptyString()][string]$LogPath,
        [Parameter(Mandatory = $true)][int]$ExitTimeoutMilliseconds,
        [Parameter(Mandatory = $true)][int]$DrainTimeoutMilliseconds
    )

    $process = $null
    $stdoutTask = $null
    $stderrTask = $null
    $stdout = ""
    $stderr = ""
    $timedOut = $false
    $terminationConfirmed = $false
    $stdoutDrained = $false
    $stderrDrained = $false
    $exitCode = $null
    $processStarted = $false
    try {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $FilePath
        $startInfo.WorkingDirectory = $WorkingDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        foreach ($argument in $ArgumentList) {
            [void]$startInfo.ArgumentList.Add($argument)
        }

        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        Assert-Condition -Condition ($process.Start()) -Message "Cannot start $Label."
        $processStarted = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $terminationConfirmed = $process.WaitForExit($ExitTimeoutMilliseconds)
        if (-not $terminationConfirmed) {
            $timedOut = $true
            try {
                $process.Kill($true)
                $terminationConfirmed = $process.WaitForExit(5000)
            }
            catch {
                $terminationConfirmed = $false
            }
        }
        if ($terminationConfirmed) {
            $exitCode = $process.ExitCode
        }

        try {
            $stdoutDrained = $stdoutTask.Wait($DrainTimeoutMilliseconds)
            if ($stdoutDrained) {
                $stdout = $stdoutTask.GetAwaiter().GetResult()
            }
        }
        catch {
            $stdout = "[$Label watchdog] stdout drainage failed: $($_.Exception.Message)"
        }
        try {
            $stderrDrained = $stderrTask.Wait($DrainTimeoutMilliseconds)
            if ($stderrDrained) {
                $stderr = $stderrTask.GetAwaiter().GetResult()
            }
        }
        catch {
            $stderr = "[$Label watchdog] stderr drainage failed: $($_.Exception.Message)"
        }

        $output = [Collections.Generic.List[string]]::new()
        foreach ($streamText in @($stdout, $stderr)) {
            foreach ($line in @($streamText -split '\r?\n')) {
                if (-not [string]::IsNullOrWhiteSpace($line)) {
                    $output.Add($line)
                }
            }
        }
        if ($timedOut) {
            $output.Add("[$Label watchdog] process exceeded $ExitTimeoutMilliseconds ms and was terminated.")
        }
        if (-not $stdoutDrained) {
            $output.Add("[$Label watchdog] stdout did not drain within $DrainTimeoutMilliseconds ms.")
        }
        if (-not $stderrDrained) {
            $output.Add("[$Label watchdog] stderr did not drain within $DrainTimeoutMilliseconds ms.")
        }
        if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
            [IO.File]::WriteAllLines($LogPath, [string[]]$output, [Text.UTF8Encoding]::new($false))
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            TimedOut = $timedOut
            TerminationConfirmed = $terminationConfirmed
            StdoutDrained = $stdoutDrained
            StderrDrained = $stderrDrained
            Output = @($output)
        }
    }
    finally {
        if ($null -ne $process) {
            if ($processStarted -and -not $process.HasExited) {
                try {
                    $process.Kill($true)
                    [void]$process.WaitForExit(5000)
                }
                catch {
                    # The bounded result records that termination could not be confirmed.
                }
            }
            $process.Dispose()
        }
    }
}

function Wait-ForReadyJson {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $timer = [Diagnostics.Stopwatch]::StartNew()
    $lastReadError = "file not observed"
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ($Process.HasExited) {
            throw "forge_runtime exited with code $($Process.ExitCode) before publishing the ready ledger."
        }
        if ([IO.File]::Exists($Path)) {
            try {
                return [IO.File]::ReadAllText($Path) | ConvertFrom-Json -Depth 64
            }
            catch {
                $lastReadError = $_.Exception.Message
            }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out after $TimeoutSeconds seconds waiting for $Path; last read error: $lastReadError"
}

function Get-DisallowedRuntimeLogLines {
    param([Parameter(Mandatory = $true)][string]$Path)

    $issues = [Collections.Generic.List[string]]::new()
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        try {
            $event = $line | ConvertFrom-Json -Depth 32
            if ([string]$event.level -in @("WARN", "ERROR")) {
                $issues.Add($line)
            }
        }
        catch {
            if ($line -match '(?i)\b(warn(?:ing)?|error|panic(?:ked)?|fatal|fail(?:ed|ure)?)\b') {
                $issues.Add($line)
            }
        }
    }
    return @($issues)
}

function Get-RuntimeEventCount {
    param(
        [Parameter(Mandatory = $true)][string[]]$Paths,
        [Parameter(Mandatory = $true)][string]$EventKind
    )

    $count = 0
    foreach ($path in $Paths) {
        foreach ($line in [IO.File]::ReadAllLines($path)) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            try {
                $event = $line | ConvertFrom-Json -Depth 32
                if ([string]::Equals([string]$event.event_kind, $EventKind, [StringComparison]::Ordinal)) {
                    $count++
                }
            }
            catch {
            }
        }
    }
    return $count
}

function Get-PresentMonLogAssessment {
    param(
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath
    )

    $stderrText = [IO.File]::ReadAllText($StderrPath).Trim()
    $normalizedStderr = [regex]::Replace($stderrText, '\s+', ' ')
    $allowedWarning = "warning: PresentMon requires elevated privilege in order to query processes that are short-running or started on another account. Without it, those processes will be listed as '<unknown>' and they can't be targeted by --process_name nor trigger --terminate_on_proc_exit."
    $allowedCount = 0
    $remainingStderr = $normalizedStderr
    $offset = $remainingStderr.IndexOf($allowedWarning, [StringComparison]::Ordinal)
    if ($offset -ge 0) {
        $allowedCount = 1
        $remainingStderr = $remainingStderr.Remove($offset, $allowedWarning.Length).Trim()
        Assert-Condition -Condition ($remainingStderr.IndexOf($allowedWarning, [StringComparison]::Ordinal) -lt 0) -Message "PresentMon emitted the allowed non-admin warning more than once."
    }
    $issues = [Collections.Generic.List[string]]::new()
    $etwStatusCount = 0
    if (-not [string]::IsNullOrWhiteSpace($remainingStderr)) {
        $issues.Add("stderr: $remainingStderr")
    }
    foreach ($line in [IO.File]::ReadAllLines($StdoutPath)) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        $match = [regex]::Match($trimmed, '^\[ETW Status\] BufferFillPct=(?<fill>\d+(?:\.\d+)?)% BuffersInUse=(?<inuse>\d+) TotalBuffers=(?<total>\d+) EventsLost=0 BuffersLost=0$')
        if (-not $match.Success) {
            $issues.Add("stdout: $trimmed")
            continue
        }
        $fill = ConvertTo-InvariantDouble -Value $match.Groups["fill"].Value -Field "PresentMon stdout ETW fill" -Minimum 0.0
        $inUse = [uint64]::Parse($match.Groups["inuse"].Value, [Globalization.CultureInfo]::InvariantCulture)
        $total = [uint64]::Parse($match.Groups["total"].Value, [Globalization.CultureInfo]::InvariantCulture)
        if ($fill -ge 100.0 -or $total -eq 0 -or $inUse -gt $total) {
            $issues.Add("stdout ETW status is invalid: $trimmed")
        }
        else {
            $etwStatusCount++
        }
    }
    return [pscustomobject]@{
        AllowedNonAdminWarningCount = $allowedCount
        EtwStatusCount = $etwStatusCount
        DisallowedTokens = @($issues)
    }
}

function Get-NearestRankPercentile {
    param(
        [Parameter(Mandatory = $true)][double[]]$SortedValues,
        [Parameter(Mandatory = $true)][ValidateRange(0.0, 1.0)][double]$Quantile
    )

    Assert-Condition -Condition ($SortedValues.Count -gt 0) -Message "Cannot compute a percentile from an empty sample."
    $index = [Math]::Max(0, [Math]::Ceiling($Quantile * $SortedValues.Count) - 1)
    return $SortedValues[[int]$index]
}

function Get-ThresholdSequences {
    param(
        [Parameter(Mandatory = $true)][object[]]$Rows,
        [Parameter(Mandatory = $true)][double]$ThresholdMilliseconds
    )

    $sequences = [Collections.Generic.List[object]]::new()
    $start = -1
    $maximum = 0.0
    for ($index = 0; $index -lt $Rows.Count; $index++) {
        $value = [double]$Rows[$index].DisplayedTimeMs
        if ($value -gt $ThresholdMilliseconds) {
            if ($start -lt 0) {
                $start = $index
                $maximum = $value
            }
            else {
                $maximum = [Math]::Max($maximum, $value)
            }
            continue
        }
        if ($start -ge 0) {
            $sequences.Add([pscustomobject]@{
                    first_sample_index = $start
                    first_csv_row = $Rows[$start].CsvRow
                    length = $index - $start
                    maximum_ms = $maximum
                })
            $start = -1
            $maximum = 0.0
        }
    }
    if ($start -ge 0) {
        $sequences.Add([pscustomobject]@{
                first_sample_index = $start
                first_csv_row = $Rows[$start].CsvRow
                length = $Rows.Count - $start
                maximum_ms = $maximum
            })
    }
    return @($sequences)
}

function Get-ArtifactDescriptor {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $file = Get-Item -LiteralPath $Path
    return [ordered]@{
        relative_path = $RelativePath
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function ConvertFrom-Gate1PresentMonEtwSessionList {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Lines
    )

    $found = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase
    )
    foreach ($line in $Lines) {
        $match = [regex]::Match(
            [string]$line,
            '^\s*(OpenRPGForge-Gate1-(?:Stutter|Diagnostic)-[0-9A-Fa-f]{32})(?=\s|$)',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor
                [Text.RegularExpressions.RegexOptions]::IgnoreCase
        )
        if ($match.Success) {
            [void]$found.Add($match.Groups[1].Value)
        }
    }
    return [string[]]@($found | Sort-Object)
}

function Get-ActiveGate1PresentMonEtwSessions {
    $command = Get-Command logman.exe -CommandType Application -ErrorAction Stop |
        Select-Object -First 1
    $query = Invoke-CapturedProcessBounded `
        -FilePath $command.Source `
        -ArgumentList @("query", "-ets") `
        -WorkingDirectory ([Environment]::SystemDirectory) `
        -Label "Gate 1 ETW session query" `
        -ExitTimeoutMilliseconds 5000 `
        -DrainTimeoutMilliseconds 2000
    Assert-Condition -Condition (-not $query.TimedOut -and $query.TerminationConfirmed) -Message "Cannot enumerate active ETW sessions inside the bounded process window."
    Assert-Condition -Condition ($query.StdoutDrained -and $query.StderrDrained) -Message "ETW session query output did not drain inside the bounded process window."
    Assert-Condition -Condition ($query.ExitCode -eq 0) -Message "Cannot enumerate active ETW sessions: logman exit code $($query.ExitCode)."
    return [string[]]@(ConvertFrom-Gate1PresentMonEtwSessionList -Lines @($query.Output))
}

function Assert-NoPreexistingGate1PresentMonEtwSession {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$ActiveSessions
    )

    if ($ActiveSessions.Count -gt 0) {
        $names = ($ActiveSessions | Sort-Object -Unique | ForEach-Object { "'$_'" }) -join ", "
        throw "Gate 1 ETW preflight refused: pre-existing PresentMon session(s): $names. No session was stopped. Verify ownership and explicitly terminate only the exact stale session before retrying."
    }
}

function Assert-OwnedGate1PresentMonEtwSessionAbsent {
    param(
        [Parameter(Mandatory = $true)][string]$SessionId,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$ActiveSessions,
        [Parameter(Mandatory = $true)][string]$Phase
    )

    if ($ActiveSessions -contains $SessionId) {
        throw "Gate 1 ETW $Phase failed: owned session '$SessionId' is still active. No unrelated session was stopped."
    }
}

function Invoke-Gate1PresentMonEtwSessionGuardSelfTest {
    $stutter = "OpenRPGForge-Gate1-Stutter-85a698103aaf4186a480beabd1840099"
    $diagnostic = "OpenRPGForge-Gate1-Diagnostic-86cd2791ab9e4398b2582dd8cce8e1fe"
    $mixedCase = "openrpgforge-GATE1-StUtTeR-a5a698103aaf4186a480beabd1840099"
    $parsed = @(ConvertFrom-Gate1PresentMonEtwSessionList -Lines @(
            "header",
            "  $diagnostic Trace Running",
            "OpenRPGForge-Gate1-Stutter-not-a-guid Trace Running",
            "$stutter Trace Running",
            "$stutter Trace Running",
            "$mixedCase Trace Running",
            "UnrelatedSession Trace Running"
        ))
    Assert-Condition -Condition ($parsed.Count -eq 3 -and $parsed -contains $stutter -and $parsed -contains $diagnostic -and $parsed -contains $mixedCase) -Message "ETW session parser self-test failed."
    Assert-NoPreexistingGate1PresentMonEtwSession -ActiveSessions @()

    $refused = $false
    try {
        Assert-NoPreexistingGate1PresentMonEtwSession -ActiveSessions @($stutter, $diagnostic)
    }
    catch {
        $refused = $_.Exception.Message.Contains($stutter) -and $_.Exception.Message.Contains("No session was stopped")
    }
    Assert-Condition -Condition $refused -Message "ETW preflight refusal self-test failed."
    Assert-OwnedGate1PresentMonEtwSessionAbsent -SessionId $stutter -ActiveSessions @($diagnostic) -Phase "postflight"

    $detected = $false
    try {
        Assert-OwnedGate1PresentMonEtwSessionAbsent -SessionId $stutter -ActiveSessions @($stutter, $diagnostic) -Phase "postflight"
    }
    catch {
        $detected = $_.Exception.Message.Contains($stutter)
    }
    Assert-Condition -Condition $detected -Message "ETW postflight self-test failed."
}

Assert-Condition -Condition ([OperatingSystem]::IsWindows()) -Message "Gate 1 PresentMon acquisition requires Windows."
Invoke-Gate1PresentationRouteSelfTest
Invoke-Gate1DisplayedStutterThresholdSelfTest
Invoke-Gate1NativeWindowContinuitySelfTest
Invoke-PresentMonStreamParserSelfTest -ExpectedHeader $expectedPresentMonHeader -ExpectedColumnCount $expectedPresentMonColumns.Count -ExpectedRuntime $expectedPresentRuntime
Invoke-PresentMonControllerQpcWindowSelfTest
Invoke-Gate1CapturedProcessOutputSelfTest
Invoke-Gate1PresentMonEtwSessionGuardSelfTest
Assert-NoPreexistingGate1PresentMonEtwSession -ActiveSessions @(Get-ActiveGate1PresentMonEtwSessions)
$presentationContract = Get-Gate1PresentationRouteContract -Route $PresentationRoute
Assert-Condition -Condition ([IO.Path]::IsPathRooted($OutputRoot)) -Message "OutputRoot must be an absolute external path."
Assert-Condition -Condition ([IO.Path]::IsPathRooted($RuntimeBinary)) -Message "RuntimeBinary must be an absolute prospective Cargo release path."
$repositoryPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$assetDirectory = Join-Path $repositoryPath "assets"
$runtimePath = Get-FullPathWithoutResolving -Path $RuntimeBinary
$presentMonFullPath = (Resolve-Path -LiteralPath $PresentMonPath).Path
$outputPath = Get-FullPathWithoutResolving -Path $OutputRoot
Assert-Condition -Condition ([string]::Equals([IO.Path]::GetFileName($runtimePath), "forge_runtime.exe", [StringComparison]::OrdinalIgnoreCase)) -Message "RuntimeBinary must be forge_runtime.exe."
$releaseDirectory = [IO.Path]::GetDirectoryName($runtimePath)
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($releaseDirectory)) -Message "RuntimeBinary has no release directory."
Assert-Condition -Condition ([string]::Equals([IO.Path]::GetFileName($releaseDirectory), "release", [StringComparison]::OrdinalIgnoreCase)) -Message "RuntimeBinary must come from a directory named release."
$cargoTargetPath = [IO.Path]::GetDirectoryName($releaseDirectory)
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($cargoTargetPath)) -Message "RuntimeBinary has no Cargo target directory."
Assert-Condition -Condition ([IO.File]::Exists($presentMonFullPath)) -Message "PresentMonPath must name a file."
Assert-Condition -Condition (-not [IO.File]::Exists($outputPath)) -Message "OutputRoot must be a directory, not a file."
Assert-Condition -Condition ([IO.Directory]::Exists($assetDirectory)) -Message "The governed repository asset directory is absent."
Assert-NoReparsePointInExistingAncestors -Path $assetDirectory

$gitTopLevel = (& git -C $repositoryPath rev-parse --show-toplevel).Trim()
Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Cannot resolve the repository root."
$gitTopLevel = [IO.Path]::GetFullPath($gitTopLevel)
Assert-Condition -Condition ([string]::Equals($repositoryPath.TrimEnd('\'), $gitTopLevel.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) -Message "RepoRoot must be the exact Git worktree root."
$gitCommit = (& git -C $repositoryPath rev-parse HEAD).Trim()
Assert-Condition -Condition ($LASTEXITCODE -eq 0 -and $gitCommit -match '^[0-9a-f]{40}$') -Message "Cannot resolve the exact repository commit."
$gitTree = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
Assert-Condition -Condition ($LASTEXITCODE -eq 0 -and $gitTree -match '^[0-9a-f]{40}$') -Message "Cannot resolve the exact repository tree."
$gitStatusBefore = @(& git -C $repositoryPath status --porcelain=v1 --untracked-files=all)
Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Cannot inspect the repository worktree."
Assert-Condition -Condition ($gitStatusBefore.Count -eq 0) -Message "Gate 1 gameplay stutter acquisition requires a clean exact-commit worktree."
$worktreeList = @(& git -C $repositoryPath worktree list --porcelain)
Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Cannot enumerate registered Git worktrees."
$registeredWorktreePaths = @($worktreeList | Where-Object { $_.StartsWith("worktree ", [StringComparison]::Ordinal) } | ForEach-Object {
        [IO.Path]::GetFullPath($_.Substring("worktree ".Length))
    })
Assert-Condition -Condition ($registeredWorktreePaths.Count -gt 0) -Message "Git did not report any registered worktree."
foreach ($registeredWorktreePath in $registeredWorktreePaths) {
    Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $registeredWorktreePath -Candidate $outputPath)) -Message "OutputRoot must be outside every registered Git worktree; it overlaps $registeredWorktreePath."
    Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $registeredWorktreePath -Candidate $cargoTargetPath)) -Message "The governed Cargo target directory must be outside every registered Git worktree; it overlaps $registeredWorktreePath."
}
Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $cargoTargetPath -Candidate $outputPath)) -Message "OutputRoot must not be inside the governed Cargo target directory."
Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $outputPath -Candidate $cargoTargetPath)) -Message "The governed Cargo target directory must not be inside OutputRoot."
Assert-NoReparsePointInExistingAncestors -Path $outputPath
Assert-NoReparsePointInExistingAncestors -Path $cargoTargetPath
Assert-Condition -Condition (-not [IO.File]::Exists($cargoTargetPath)) -Message "The governed Cargo target path must not be a file."
if ([IO.Directory]::Exists($cargoTargetPath)) {
    Assert-Condition -Condition (@([IO.Directory]::EnumerateFileSystemEntries($cargoTargetPath)).Count -eq 0) -Message "The governed Cargo target directory must be empty to prevent stale artifact reuse."
}
if ([IO.Directory]::Exists($outputPath)) {
    Assert-Condition -Condition (@([IO.Directory]::EnumerateFileSystemEntries($outputPath)).Count -eq 0) -Message "OutputRoot must be empty to prevent stale evidence reuse."
}

$presentMonFile = Get-Item -LiteralPath $presentMonFullPath
Assert-Condition -Condition ($presentMonFile.Length -eq $presentMonBytes) -Message "PresentMon.exe size is $($presentMonFile.Length), expected $presentMonBytes bytes."
$observedPresentMonSha256 = (Get-FileHash -LiteralPath $presentMonFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Condition -Condition ([string]::Equals($observedPresentMonSha256, $presentMonSha256, [StringComparison]::Ordinal)) -Message "PresentMon.exe SHA-256 does not match the pinned 2.4.1 binary."
$presentMonVersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($presentMonFullPath)
$presentMonVersionResourcePresent = -not [string]::IsNullOrWhiteSpace($presentMonVersionInfo.FileVersion) -or
    -not [string]::IsNullOrWhiteSpace($presentMonVersionInfo.ProductVersion)
Assert-Condition -Condition (-not $presentMonVersionResourcePresent) -Message "The pinned PresentMon 2.4.1 asset unexpectedly contains a Windows version resource."
$presentMonSignature = Get-AuthenticodeSignature -LiteralPath $presentMonFullPath
Assert-Condition -Condition ($presentMonSignature.Status -eq [Management.Automation.SignatureStatus]::Valid) -Message "PresentMon.exe Authenticode signature is not valid: $($presentMonSignature.Status)."
Assert-Condition -Condition ($null -ne $presentMonSignature.SignerCertificate) -Message "PresentMon.exe has no signer certificate."
$presentMonSigner = $presentMonSignature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
Assert-Condition -Condition ([string]::Equals($presentMonSigner, "Intel Corporation", [StringComparison]::Ordinal)) -Message "PresentMon.exe signer is '$presentMonSigner', expected Intel Corporation."
Assert-Condition -Condition ($presentMonSignature.SignerCertificate.Subject -match '(^|,\s*)O=Intel Corporation(,|$)') -Message "PresentMon.exe signer organization is not Intel Corporation."

$cargoCommand = Get-Command cargo -CommandType Application -ErrorAction Stop | Select-Object -First 1
Assert-Condition -Condition ($null -ne $cargoCommand -and [IO.File]::Exists($cargoCommand.Source)) -Message "Cannot resolve the Cargo executable."
$cargoVersion = (& $cargoCommand.Source --version).Trim()
Assert-Condition -Condition ($LASTEXITCODE -eq 0 -and $cargoVersion -match '^cargo 1\.98\.') -Message "Gate 1 acquisition requires the pinned Cargo 1.98 toolchain."
$previousCargoTargetDir = [Environment]::GetEnvironmentVariable("CARGO_TARGET_DIR", "Process")
$previousSourceCommit = [Environment]::GetEnvironmentVariable("FORGE_SOURCE_COMMIT", "Process")
$cargoBuildExitCode = $null
$cargoLocationPushed = $false
try {
    $env:CARGO_TARGET_DIR = $cargoTargetPath
    $env:FORGE_SOURCE_COMMIT = $gitCommit
    Push-Location -LiteralPath $repositoryPath
    $cargoLocationPushed = $true
    & $cargoCommand.Source build -p forge_runtime --release --locked -j 1
    $cargoBuildExitCode = $LASTEXITCODE
}
finally {
    if ($cargoLocationPushed) {
        Pop-Location
    }
    if ($null -eq $previousCargoTargetDir) {
        Remove-Item Env:CARGO_TARGET_DIR -ErrorAction SilentlyContinue
    }
    else {
        $env:CARGO_TARGET_DIR = $previousCargoTargetDir
    }
    if ($null -eq $previousSourceCommit) {
        Remove-Item Env:FORGE_SOURCE_COMMIT -ErrorAction SilentlyContinue
    }
    else {
        $env:FORGE_SOURCE_COMMIT = $previousSourceCommit
    }
}
Assert-Condition -Condition ($cargoBuildExitCode -eq 0) -Message "The governed Cargo release build failed with exit code $cargoBuildExitCode."
Assert-Condition -Condition ([IO.File]::Exists($runtimePath)) -Message "The governed Cargo build did not produce RuntimeBinary."
$resolvedRuntimePath = (Resolve-Path -LiteralPath $runtimePath).Path
Assert-Condition -Condition ([string]::Equals($resolvedRuntimePath, $runtimePath, [StringComparison]::OrdinalIgnoreCase)) -Message "The governed Cargo build resolved RuntimeBinary through an unexpected path."
$runtimeFileAfterBuild = Get-Item -LiteralPath $runtimePath -Force
Assert-Condition -Condition (($runtimeFileAfterBuild.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "RuntimeBinary is a reparse point and cannot establish build provenance."
Assert-Condition -Condition (-not [IO.Directory]::Exists((Join-Path $releaseDirectory "assets"))) -Message "The governed build copied assets into the Cargo release directory."
$gitCommitAfterBuild = (& git -C $repositoryPath rev-parse HEAD).Trim()
$gitCommitAfterBuildExitCode = $LASTEXITCODE
Assert-Condition -Condition ($gitCommitAfterBuildExitCode -eq 0 -and $gitCommitAfterBuild -eq $gitCommit) -Message "Repository HEAD changed during the governed Cargo build."
$gitTreeAfterBuild = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
$gitTreeAfterBuildExitCode = $LASTEXITCODE
Assert-Condition -Condition ($gitTreeAfterBuildExitCode -eq 0 -and $gitTreeAfterBuild -eq $gitTree) -Message "Repository tree changed during the governed Cargo build."
$gitStatusAfterBuild = @(& git -C $repositoryPath status --porcelain=v1 --untracked-files=all)
$gitStatusAfterBuildExitCode = $LASTEXITCODE
Assert-Condition -Condition ($gitStatusAfterBuildExitCode -eq 0 -and $gitStatusAfterBuild.Count -eq 0) -Message "Repository worktree changed during the governed Cargo build."
$runtimeBuildCompletedAtUtc = [DateTimeOffset]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture)

if ([IO.Directory]::Exists($outputPath)) {
    Assert-Condition -Condition (@([IO.Directory]::EnumerateFileSystemEntries($outputPath)).Count -eq 0) -Message "OutputRoot must be empty to prevent stale evidence reuse."
}
else {
    [void][IO.Directory]::CreateDirectory($outputPath)
}
Assert-NoReparsePointInExistingAncestors -Path $outputPath

$readyPath = Join-Path $outputPath $readyFileName
$signalPath = Join-Path $outputPath $signalFileName
$hostBoundaryPath = Join-Path $outputPath $hostBoundaryFileName
$ledgerPath = Join-Path $outputPath $ledgerFileName
$csvPath = Join-Path $outputPath "presentmon.csv"
$runtimeStdoutPath = Join-Path $outputPath "runtime.stdout.log"
$runtimeStderrPath = Join-Path $outputPath "runtime.stderr.log"
$presentMonRawStdoutPath = Join-Path $outputPath "presentmon.raw.stdout.log"
$presentMonStdoutPath = Join-Path $outputPath "presentmon.stdout.log"
$presentMonStderrPath = Join-Path $outputPath "presentmon.stderr.log"
$presentMonStopPath = Join-Path $outputPath "presentmon.stop.log"
$presentMonFallbackStopPath = Join-Path $outputPath "presentmon.fallback-stop.log"
$validationPath = Join-Path $outputPath $validationFileName
$fifoDiagnosticMarkerPath = Join-Path $outputPath $fifoDiagnosticMarkerFileName
$runtimeCapture = $null
$presentMonCapture = $null
$runtimeOutput = $null
$presentMonOutput = $null
$nativeWindowObservation = $null
$presentMonSessionStopped = $false
$presentMonStopExitCode = $null
$presentMonStopResult = $null
$foregroundAttempt = $null
$nativeWindowSamples = [Collections.Generic.List[object]]::new()
$runtimeSha256 = (Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash.ToLowerInvariant()
$sessionId = "OpenRPGForge-Gate1-Stutter-$([Guid]::NewGuid().ToString('N'))"
$presentMonSessionOwned = $false
$etwGuardMutex = $null
$etwGuardMutexHeld = $false

try {
    $etwGuardMutex = [Threading.Mutex]::new($false, "Global\OpenRPGForge-Gate1-PresentMon-Guard")
    try {
        $etwGuardMutexHeld = $etwGuardMutex.WaitOne(0)
    }
    catch [Threading.AbandonedMutexException] {
        $etwGuardMutexHeld = $true
    }
    Assert-Condition -Condition $etwGuardMutexHeld -Message "Another Gate 1 acquisition owns the PresentMon ETW guard."
    Assert-NoPreexistingGate1PresentMonEtwSession -ActiveSessions @(Get-ActiveGate1PresentMonEtwSessions)

    $runtimeArguments = @(
        "--visual-valley",
        "--quality", "high",
        "--gate1-gameplay-stutter", $outputPath
    )
    $runtimeArguments += @($presentationContract.RuntimeArguments)
    $runtimeEnvironment = @{
        RUST_LOG = "info,forge=info,wgpu_hal=warn,wgpu_core=warn,naga=warn"
        WGPU_BACKEND = "vulkan"
        BEVY_ASSET_ROOT = $repositoryPath
    }
    $runtimeCapture = Start-CapturedProcess -FilePath $runtimePath -ArgumentList $runtimeArguments -WorkingDirectory $repositoryPath -Label "forge_runtime Gate 1 gameplay stutter" -StdoutPath $runtimeStdoutPath -StderrPath $runtimeStderrPath -Visible $true -EnvironmentVariables $runtimeEnvironment
    $runtimeProcessId = $runtimeCapture.Process.Id
    $runtimeWatchdog = [Diagnostics.Stopwatch]::StartNew()

    $ready = Wait-ForReadyJson -Path $readyPath -Process $runtimeCapture.Process -TimeoutSeconds $readyTimeoutSeconds
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "schema" -Path "ready") -eq "forge.gate1-gameplay-stutter-ready") -Message "Unexpected ready schema."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "schema_version" -Path "ready") -eq 4) -Message "Unexpected ready schema version."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "status" -Path "ready") -eq "ready_waiting_for_presentmon_start_signal") -Message "Runtime is not waiting for the PresentMon start signal."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "process_id" -Path "ready") -eq $runtimeProcessId) -Message "Ready PID does not match the launched runtime PID."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "build_source_commit" -Path "ready") -eq $gitCommit) -Message "Ready ledger is not linked to the exact repository commit."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ready -Name "build_profile" -Path "ready") -eq "release") -Message "Runtime does not declare the exact Cargo release profile."

    $readyProtocol = Get-RequiredProperty -InputObject $ready -Name "protocol" -Path "ready"
    Assert-Condition -Condition (((Get-RequiredProperty -InputObject $readyProtocol -Name "physical_resolution_px" -Path "ready.protocol") -join "x") -eq "1920x1080") -Message "Ready resolution is not 1920x1080."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "requested_present_mode" -Path "ready.protocol") -eq $presentationContract.RequestedPresentMode) -Message "Ready requested present mode does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "presentation_route" -Path "ready.protocol") -eq $presentationContract.PresentationRoute) -Message "Ready presentation route does not match $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "vsync_requested" -Path "ready.protocol") -eq $presentationContract.VsyncRequested) -Message "Ready VSync request does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "qualification_eligible" -Path "ready.protocol") -eq $presentationContract.QualificationEligible) -Message "Ready qualification eligibility does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "observation_scope" -Path "ready.protocol") -eq "bevy_window_request_not_resolved_surface") -Message "Ready presentation observation scope changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "fixed_hz" -Path "ready.protocol") -eq 60) -Message "Ready fixed rate is not 60 Hz."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "post_measurement_window_hold_ms" -Path "ready.protocol") -eq 500) -Message "Ready post-measurement window hold is not 500 ms."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "no_pause" -Path "ready.protocol") -eq $true) -Message "Ready protocol permits pausing."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "screenshots_requested" -Path "ready.protocol") -eq $false) -Message "Ready protocol requests screenshots."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "presentmon_required" -Path "ready.protocol") -eq $true) -Message "Ready protocol does not require PresentMon."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "vegetation_visibility_audit_interval_ms" -Path "ready.protocol") -eq 100) -Message "Ready protocol vegetation visibility audit cadence changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "ready_vegetation_visibility_audit_receipt_required" -Path "ready.protocol") -eq $true) -Message "Ready protocol does not require a vegetation visibility audit receipt."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyProtocol -Name "final_vegetation_visibility_audit_receipt_required" -Path "ready.protocol") -eq $true) -Message "Ready protocol does not require a final vegetation visibility audit receipt."
    Assert-QualifyingInstrumentationContract -Instrumentation (Get-RequiredProperty -InputObject $readyProtocol -Name "instrumentation" -Path "ready.protocol") -Path "ready.protocol.instrumentation"
    $readySignalContract = Get-RequiredProperty -InputObject $ready -Name "start_signal_contract" -Path "ready"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readySignalContract -Name "schema_version" -Path "ready.start_signal_contract") -eq 3) -Message "Ready start-signal contract schema version changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readySignalContract -Name "qpc_boundary_required" -Path "ready.start_signal_contract") -eq $true) -Message "Ready contract does not require an observed QPC boundary."

    $readyVisibilityAudit = Get-RequiredProperty -InputObject $ready -Name "vegetation_visibility_audit" -Path "ready"
    [uint64]$readyVisibilityAuditTicket = Get-RequiredProperty -InputObject $readyVisibilityAudit -Name "ticket_id" -Path "ready.vegetation_visibility_audit"
    [uint64]$readyVisibilityAuditCompleted = Get-RequiredProperty -InputObject $readyVisibilityAudit -Name "completed_id" -Path "ready.vegetation_visibility_audit"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyVisibilityAudit -Name "interval_ms" -Path "ready.vegetation_visibility_audit") -eq 100) -Message "Ready vegetation visibility audit interval changed."
    Assert-Condition -Condition ($readyVisibilityAuditTicket -gt 0) -Message "Ready vegetation visibility audit ticket is absent."
    Assert-Condition -Condition ($readyVisibilityAuditCompleted -ge $readyVisibilityAuditTicket) -Message "Ready vegetation visibility audit receipt does not cover its ticket."

    $readyPipeline = Get-RequiredProperty -InputObject $ready -Name "pipeline_warmup" -Path "ready"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyPipeline -Name "status" -Path "ready.pipeline_warmup") -eq "ready") -Message "Pipelines are not ready before PresentMon attachment."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyPipeline -Name "errors" -Path "ready.pipeline_warmup") -eq 0) -Message "Pipeline warmup reports errors."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyPipeline -Name "stable_frames" -Path "ready.pipeline_warmup") -ge (Get-RequiredProperty -InputObject $readyPipeline -Name "required_stable_frames" -Path "ready.pipeline_warmup")) -Message "Pipeline warmup is not stable."
    $readyQuality = Get-RequiredProperty -InputObject $ready -Name "quality" -Path "ready"
    $readySelection = Get-RequiredProperty -InputObject $readyQuality -Name "selection" -Path "ready.quality"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readySelection -Name "mode" -Path "ready.quality.selection") -eq "fixed") -Message "Ready quality is not fixed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readySelection -Name "tier" -Path "ready.quality.selection") -eq "high") -Message "Ready quality is not High."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyQuality -Name "current_tier" -Path "ready.quality") -eq "high") -Message "Ready current quality is not High."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyQuality -Name "adaptation_enabled" -Path "ready.quality") -eq $false) -Message "Quality adaptation is enabled."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyQuality -Name "transition_count" -Path "ready.quality") -eq 0) -Message "Quality changed before measurement."
    $readyScenery = Get-RequiredProperty -InputObject $ready -Name "scenery" -Path "ready"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyScenery -Name "loaded_models" -Path "ready.scenery") -eq 9) -Message "Ready scenery did not load exactly 9 models."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyScenery -Name "unique_models" -Path "ready.scenery") -eq 9) -Message "Ready scenery does not describe exactly 9 models."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyScenery -Name "failed_models" -Path "ready.scenery") -eq 0) -Message "Ready scenery reports failed models."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyScenery -Name "status" -Path "ready.scenery") -eq "activated") -Message "Ready scenery is not activated."
    $readyGovernedScene = Get-RequiredProperty -InputObject $ready -Name "governed_scene" -Path "ready"
    foreach ($entry in @(
            @("visual_valley", "source_hash"),
            @("vegetation", "vegetation_document_hash"),
            @("scenery", "manifest_hash"),
            @("environment", "source_hash"),
            @("water", "source_hash"),
            @("weather", "source_hash")
        )) {
        $subsystem = Get-RequiredProperty -InputObject $readyGovernedScene -Name $entry[0] -Path "ready.governed_scene"
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string](Get-RequiredProperty -InputObject $subsystem -Name $entry[1] -Path "ready.governed_scene.$($entry[0])"))) -Message "Ready governed scene $($entry[0]).$($entry[1]) is absent."
    }
    $readyGpu = Get-RequiredProperty -InputObject $ready -Name "gpu" -Path "ready"
    foreach ($field in @("name", "driver", "backend")) {
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string](Get-RequiredProperty -InputObject $readyGpu -Name $field -Path "ready.gpu"))) -Message "Ready GPU $field is absent."
    }
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyGpu -Name "backend" -Path "ready.gpu") -eq "Vulkan") -Message "Ready GPU backend is not Vulkan."
    $readyQualification = Get-RequiredProperty -InputObject $ready -Name "qualification" -Path "ready"
    foreach ($field in @("display_stutter_qualified", "human_review_performed", "gate1_route_qualified")) {
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readyQualification -Name $field -Path "ready.qualification") -eq $false) -Message "Ready ledger overstates $field."
    }
    $readySources = Get-RequiredProperty -InputObject $ready -Name "sources" -Path "ready"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $readySources -Name "cadle_assets_used" -Path "ready.sources") -eq $false) -Message "Ready ledger reports Cadle asset use."

    if ($presentationContract.MarkerRequired) {
        Assert-Condition -Condition (Test-Path -LiteralPath $fifoDiagnosticMarkerPath -PathType Leaf) -Message "$fifoDiagnosticMarkerFileName is absent for the FIFO diagnostic route."
        $fifoDiagnosticMarker = Get-Content -Raw -LiteralPath $fifoDiagnosticMarkerPath | ConvertFrom-Json -Depth 64
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "schema" -Path "fifo_marker") -eq "forge.nonqualifying-gate1-fifo-diagnostic") -Message "Unexpected FIFO diagnostic marker schema."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "schema_version" -Path "fifo_marker") -eq 1) -Message "Unexpected FIFO diagnostic marker schema version."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "qualifying" -Path "fifo_marker") -eq $false) -Message "FIFO diagnostic marker overstates qualification."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "source_commit" -Path "fifo_marker") -eq $gitCommit) -Message "FIFO diagnostic marker source commit mismatch."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "presentation_route" -Path "fifo_marker") -eq "fifo_diagnostic") -Message "FIFO diagnostic marker route mismatch."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "requested_present_mode" -Path "fifo_marker") -eq "fifo") -Message "FIFO diagnostic marker requested mode mismatch."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "vsync_requested" -Path "fifo_marker") -eq $true) -Message "FIFO diagnostic marker does not record VSync."
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "observation_scope" -Path "fifo_marker") -eq "bevy_window_request_not_resolved_surface") -Message "FIFO diagnostic marker observation scope mismatch."
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string](Get-RequiredProperty -InputObject $fifoDiagnosticMarker -Name "reason" -Path "fifo_marker"))) -Message "FIFO diagnostic marker reason is absent."
    }
    else {
        Assert-Condition -Condition (-not (Test-Path -LiteralPath $fifoDiagnosticMarkerPath)) -Message "FIFO diagnostic marker exists on the AutoNoVsync route."
    }

    $nativeWindowObservation = Wait-ForQualifiedNativeWindow -Process $runtimeCapture.Process -TimeoutSeconds 5
    $presentMonArguments = @(
        "--process_id", [string]$runtimeProcessId,
        "--output_stdout",
        "--v2_metrics",
        "--qpc_time",
        "--track_etw_status",
        "--session_name", $sessionId,
        "--no_console_stats",
        "--set_circular_buffer_size", "4096"
    )
    Assert-NoPreexistingGate1PresentMonEtwSession -ActiveSessions @(Get-ActiveGate1PresentMonEtwSessions)
    $presentMonSessionOwned = $true
    $presentMonCapture = Start-CapturedProcess -FilePath $presentMonFullPath -ArgumentList $presentMonArguments -WorkingDirectory $outputPath -Label "PresentMon Gate 1 gameplay stutter" -StdoutPath $presentMonRawStdoutPath -StderrPath $presentMonStderrPath -Visible $false -StreamToFiles

    $presentMonReadyTimer = [Diagnostics.Stopwatch]::StartNew()
    $presentMonHeaderObserved = $false
    while ($presentMonReadyTimer.Elapsed.TotalSeconds -lt $presentMonReadyTimeoutSeconds) {
        if ($presentMonCapture.Process.HasExited) {
            throw "PresentMon exited with code $($presentMonCapture.Process.ExitCode) before publishing its CSV header."
        }
        if ([IO.File]::Exists($presentMonRawStdoutPath)) {
            try {
                $streamSnapshot = Get-PresentMonStreamSnapshot -Path $presentMonRawStdoutPath -ExpectedHeader $expectedPresentMonHeader
                if ($streamSnapshot.HeaderObserved) {
                    Assert-Condition -Condition ($streamSnapshot.UnknownLines.Count -eq 0) -Message "PresentMon stdout emitted unknown content before its exact governed CSV header."
                    $presentMonHeaderObserved = $true
                    break
                }
            }
            catch {
                if ($_.Exception.Message -like "PresentMon CSV header is not*" -or $_.Exception.Message -like "PresentMon stdout emitted unknown content*") {
                    throw
                }
            }
        }
        Start-Sleep -Milliseconds 100
    }
    Assert-Condition -Condition $presentMonHeaderObserved -Message "PresentMon did not publish its exact CSV header within $presentMonReadyTimeoutSeconds seconds."

    $preSignalTimer = [Diagnostics.Stopwatch]::StartNew()
    $preSignalObservedRows = 0
    [uint64]$lastPreSignalQpc = 0
    while ($preSignalTimer.Elapsed.TotalSeconds -lt $presentMonReadyTimeoutSeconds) {
        if ($presentMonCapture.Process.HasExited) {
            throw "PresentMon exited before observing a complete target-PID display row."
        }
        try {
            $streamSnapshot = Get-PresentMonStreamSnapshot -Path $presentMonRawStdoutPath -ExpectedHeader $expectedPresentMonHeader
            Assert-Condition -Condition ($streamSnapshot.UnknownLines.Count -eq 0) -Message "PresentMon stdout emitted unknown content before its exact governed CSV header."
            $candidateRows = @($streamSnapshot.Rows)
            $completeRows = @($candidateRows | Where-Object {
                    Test-QualifiedPresentMonPreSignalRow `
                        -Row $_ `
                        -ExpectedColumnCount $expectedPresentMonColumns.Count `
                        -ExpectedApplication ([IO.Path]::GetFileName($runtimePath)) `
                        -ExpectedProcessId $runtimeProcessId `
                        -ExpectedRuntime $expectedPresentRuntime
                })
            if ($completeRows.Count -gt 0) {
                $lastPreSignalRow = $completeRows[$completeRows.Count - 1]
                Assert-Condition -Condition ([string]::Equals([string]$lastPreSignalRow.Application, [IO.Path]::GetFileName($runtimePath), [StringComparison]::OrdinalIgnoreCase)) -Message "PresentMon pre-signal row names the wrong application."
                Assert-Condition -Condition ([string]$lastPreSignalRow.SwapChainAddress -match '^0x[0-9A-Fa-f]+$') -Message "PresentMon pre-signal row has no valid swap chain."
                $lastPreSignalQpc = [uint64]::Parse([string]$lastPreSignalRow.CPUStartQPC, [Globalization.CultureInfo]::InvariantCulture)
                $preSignalObservedRows = $completeRows.Count
                break
            }
        }
        catch {
            if ($_.Exception.Message -like "PresentMon pre-signal row*" -or $_.Exception.Message -like "PresentMon stdout emitted unknown content*") {
                throw
            }
        }
        Start-Sleep -Milliseconds 100
    }
    Assert-Condition -Condition ($lastPreSignalQpc -gt 0 -and $preSignalObservedRows -gt 0) -Message "PresentMon did not observe a complete target-PID row before the start signal."
    $foregroundAttempt = Invoke-Gate1RuntimeWindowForegroundAttempt -Process $runtimeCapture.Process -ExpectedHandle $nativeWindowObservation.RawHandle
    if ($null -ne $foregroundAttempt.LastState) {
        $nativeWindowSamples.Add($foregroundAttempt.LastState)
    }
    [uint64]$qpcFrequencyHz = [Diagnostics.Stopwatch]::Frequency
    Assert-Condition -Condition ($qpcFrequencyHz -gt 0) -Message "The host QPC frequency is unavailable."
    [uint64]$measurementNotBeforeQpc = [Diagnostics.Stopwatch]::GetTimestamp()
    Assert-Condition -Condition ($measurementNotBeforeQpc -gt $lastPreSignalQpc) -Message "The signal publication QPC does not follow the observed pre-signal PresentMon row."

    $signal = [ordered]@{
        schema = "forge.gate1-gameplay-stutter-start-signal"
        schema_version = 3
        process_id = $runtimeProcessId
        presentmon = [ordered]@{
            capture_started = $true
            target_process_id = $runtimeProcessId
            session_id = $sessionId
            tool_version = $presentMonVersion
            tool_sha256 = $presentMonSha256
            last_pre_signal_qpc = $lastPreSignalQpc
            measurement_not_before_qpc = $measurementNotBeforeQpc
            qpc_frequency_hz = $qpcFrequencyHz
            pre_signal_observed_rows = $preSignalObservedRows
        }
    }
    Write-JsonAtomically -Path $signalPath -Value $signal
    [uint64]$signalPublishedQpc = [Diagnostics.Stopwatch]::GetTimestamp()
    Assert-Condition -Condition ($signalPublishedQpc -ge $measurementNotBeforeQpc) -Message "The host QPC regressed while publishing the start signal."
    $hostBoundary = [ordered]@{
        schema = "forge.gate1-gameplay-stutter-host-boundary"
        schema_version = 1
        repository_commit = $gitCommit
        repository_tree = $gitTree
        runtime_process_id = $runtimeProcessId
        presentmon_session_id = $sessionId
        measurement_not_before_qpc = $measurementNotBeforeQpc
        signal_published_qpc = $signalPublishedQpc
        qpc_frequency_hz = $qpcFrequencyHz
    }
    Write-JsonAtomically -Path $hostBoundaryPath -Value $hostBoundary

    while (-not $runtimeCapture.Process.HasExited) {
        if ($presentMonCapture.Process.HasExited) {
            throw "PresentMon exited with code $($presentMonCapture.Process.ExitCode) before forge_runtime completed."
        }
        if ($runtimeWatchdog.Elapsed.TotalSeconds -ge $runtimeTimeoutSeconds) {
            throw "forge_runtime exceeded the host watchdog of $runtimeTimeoutSeconds seconds."
        }
        try {
            $nativeWindowSamples.Add((Get-Gate1NativeWindowState -Process $runtimeCapture.Process -ExpectedHandle $nativeWindowObservation.RawHandle))
        }
        catch {
            if (-not $runtimeCapture.Process.HasExited) {
                throw
            }
        }
        Start-Sleep -Milliseconds 100
    }
    Assert-Condition -Condition ($runtimeCapture.Process.ExitCode -eq 0) -Message "forge_runtime failed with exit code $($runtimeCapture.Process.ExitCode)."
    $presentMonStopResult = Invoke-CapturedProcessBounded `
        -FilePath $presentMonFullPath `
        -ArgumentList @("--session_name", $sessionId, "--terminate_existing_session") `
        -WorkingDirectory $outputPath `
        -Label "PresentMon session-stop control" `
        -LogPath $presentMonStopPath `
        -ExitTimeoutMilliseconds ($presentMonExitTimeoutSeconds * 1000) `
        -DrainTimeoutMilliseconds $drainTimeoutMilliseconds
    $presentMonStopOutput = @($presentMonStopResult.Output)
    $presentMonStopExitCode = $presentMonStopResult.ExitCode
    Assert-Condition -Condition (-not $presentMonStopResult.TimedOut -and $presentMonStopResult.TerminationConfirmed) -Message "PresentMon session-stop control did not terminate inside the bounded window."
    Assert-Condition -Condition ($presentMonStopResult.StdoutDrained -and $presentMonStopResult.StderrDrained) -Message "PresentMon session-stop control output did not drain inside the bounded window."
    Assert-Condition -Condition ($presentMonStopExitCode -eq 0) -Message "PresentMon session-stop control failed with exit code $presentMonStopExitCode."
    $allowedStopOutput = @(
        "warning: --terminate_existing_session exits without capturing anything; ignoring all other options.",
        "warning: --no_console_stats added because stdout does not support statistics reporting."
    )
    $unexpectedStopOutput = @($presentMonStopOutput | ForEach-Object { [regex]::Replace($_.Trim(), '\s+', ' ') } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -notin $allowedStopOutput })
    Assert-Condition -Condition ($unexpectedStopOutput.Count -eq 0) -Message "PresentMon session-stop control emitted unexpected output: $($unexpectedStopOutput -join '; ')."
    Assert-Condition -Condition ($presentMonCapture.Process.WaitForExit($presentMonExitTimeoutSeconds * 1000)) -Message "PresentMon did not terminate within $presentMonExitTimeoutSeconds seconds after target PID $runtimeProcessId exited."
    Assert-Condition -Condition ($presentMonCapture.Process.ExitCode -eq 0) -Message "PresentMon failed with exit code $($presentMonCapture.Process.ExitCode)."
    Assert-OwnedGate1PresentMonEtwSessionAbsent `
        -SessionId $sessionId `
        -ActiveSessions @(Get-ActiveGate1PresentMonEtwSessions) `
        -Phase "postflight"
    $presentMonSessionStopped = $true

    $runtimeOutput = Save-CapturedProcessOutput -Capture $runtimeCapture -DrainTimeoutMilliseconds $drainTimeoutMilliseconds
    $presentMonOutput = Save-CapturedProcessOutput -Capture $presentMonCapture -DrainTimeoutMilliseconds $drainTimeoutMilliseconds
    Assert-Condition -Condition ($runtimeOutput.StdoutDrained -and $runtimeOutput.StderrDrained) -Message "Runtime output did not drain inside the bounded window."
    Assert-Condition -Condition ($presentMonOutput.StdoutDrained -and $presentMonOutput.StderrDrained) -Message "PresentMon output did not drain inside the bounded window."
    $presentMonStreamPartition = Write-PresentMonStreamArtifacts -RawPath $presentMonRawStdoutPath -CsvPath $csvPath -ConsolePath $presentMonStdoutPath -ExpectedHeader $expectedPresentMonHeader

    $gitCommitAfter = (& git -C $repositoryPath rev-parse HEAD).Trim()
    $gitCommitAfterExitCode = $LASTEXITCODE
    Assert-Condition -Condition ($gitCommitAfterExitCode -eq 0 -and $gitCommitAfter -match '^[0-9a-f]{40}$') -Message "Cannot re-resolve repository HEAD after acquisition."
    $gitTreeAfter = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
    $gitTreeAfterExitCode = $LASTEXITCODE
    Assert-Condition -Condition ($gitTreeAfterExitCode -eq 0 -and $gitTreeAfter -match '^[0-9a-f]{40}$') -Message "Cannot re-resolve repository tree after acquisition."
    $gitStatusAfter = @(& git -C $repositoryPath status --porcelain=v1 --untracked-files=all)
    $gitStatusAfterExitCode = $LASTEXITCODE
    Assert-Condition -Condition ($gitStatusAfterExitCode -eq 0) -Message "Cannot inspect repository worktree after acquisition."
    Assert-Condition -Condition ($gitCommitAfter -eq $gitCommit) -Message "Repository HEAD changed during acquisition."
    Assert-Condition -Condition ($gitTreeAfter -eq $gitTree) -Message "Repository tree changed during acquisition."
    Assert-Condition -Condition ($gitStatusAfter.Count -eq 0) -Message "Repository worktree changed during acquisition."

    $runtimeLogIssues = @(
        @(Get-DisallowedRuntimeLogLines -Path $runtimeStdoutPath)
        @(Get-DisallowedRuntimeLogLines -Path $runtimeStderrPath)
    )
    Assert-Condition -Condition ($runtimeLogIssues.Count -eq 0) -Message "Runtime logs contain warning, error, panic, fatal or failure entries."
    $runtimeLogPaths = @($runtimeStdoutPath, $runtimeStderrPath)
    foreach ($eventKind in @("runtime_started", "gate1_gameplay_stutter_ready", "gate1_gameplay_stutter_controller_window_completed", "runtime_stopped")) {
        Assert-Condition -Condition ((Get-RuntimeEventCount -Paths $runtimeLogPaths -EventKind $eventKind) -eq 1) -Message "Runtime log event '$eventKind' is missing or duplicated."
    }
    $presentMonLogAssessment = Get-PresentMonLogAssessment -StdoutPath $presentMonStdoutPath -StderrPath $presentMonStderrPath
    Assert-Condition -Condition ($presentMonLogAssessment.DisallowedTokens.Count -eq 0) -Message "PresentMon logs contain a disallowed warning/error/loss/failure token."
    Assert-Condition -Condition ($presentMonLogAssessment.EtwStatusCount -gt 0) -Message "PresentMon emitted no valid ETW status line on its governed stdout stream."

    Assert-Condition -Condition ([IO.File]::Exists($ledgerPath)) -Message "Runtime did not publish $ledgerFileName."
    $ledger = [IO.File]::ReadAllText($ledgerPath) | ConvertFrom-Json -Depth 64
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "schema" -Path "ledger") -eq "forge.gate1-gameplay-stutter") -Message "Unexpected runtime ledger schema."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "schema_version" -Path "ledger") -eq 5) -Message "Unexpected runtime ledger schema version."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "status" -Path "ledger") -eq "controller_window_complete_pending_presentmon_analysis") -Message "Runtime controller window did not complete cleanly."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "process_id" -Path "ledger") -eq $runtimeProcessId) -Message "Runtime ledger PID mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "build_source_commit" -Path "ledger") -eq $gitCommit) -Message "Runtime ledger commit mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledger -Name "build_profile" -Path "ledger") -eq "release") -Message "Runtime ledger build profile is not release."
    $ledgerReady = Get-RequiredProperty -InputObject $ledger -Name "ready" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerReady -Name "signal_schema_version" -Path "ledger.ready") -eq 3) -Message "Runtime ledger start-signal schema version mismatch."
    $ledgerSources = Get-RequiredProperty -InputObject $ledger -Name "sources" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerSources -Name "cadle_assets_used" -Path "ledger.sources") -eq $false) -Message "Runtime ledger reports Cadle asset use."

    $ledgerProtocol = Get-RequiredProperty -InputObject $ledger -Name "protocol" -Path "ledger"
    Assert-Condition -Condition (((Get-RequiredProperty -InputObject $ledgerProtocol -Name "physical_resolution_px" -Path "ledger.protocol") -join "x") -eq "1920x1080") -Message "Ledger resolution is not 1920x1080."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "requested_present_mode" -Path "ledger.protocol") -eq $presentationContract.RequestedPresentMode) -Message "Ledger requested present mode does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "presentation_route" -Path "ledger.protocol") -eq $presentationContract.PresentationRoute) -Message "Ledger presentation route does not match $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "vsync_requested" -Path "ledger.protocol") -eq $presentationContract.VsyncRequested) -Message "Ledger VSync request does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "qualification_eligible" -Path "ledger.protocol") -eq $presentationContract.QualificationEligible) -Message "Ledger qualification eligibility does not match route $PresentationRoute."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "observation_scope" -Path "ledger.protocol") -eq "bevy_window_request_not_resolved_surface") -Message "Ledger presentation observation scope changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "post_measurement_window_hold_ms" -Path "ledger.protocol") -eq 500) -Message "Ledger post-measurement window hold is not 500 ms."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "no_pause" -Path "ledger.protocol") -eq $true) -Message "Ledger protocol allowed pausing."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "screenshots_requested" -Path "ledger.protocol") -eq $false) -Message "Ledger protocol requested screenshots."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "vegetation_visibility_audit_interval_ms" -Path "ledger.protocol") -eq 100) -Message "Ledger protocol vegetation visibility audit cadence changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "ready_vegetation_visibility_audit_receipt_required" -Path "ledger.protocol") -eq $true) -Message "Ledger protocol does not require the ready vegetation visibility audit receipt."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerProtocol -Name "final_vegetation_visibility_audit_receipt_required" -Path "ledger.protocol") -eq $true) -Message "Ledger protocol does not require the final vegetation visibility audit receipt."
    $ledgerInstrumentation = Get-RequiredProperty -InputObject $ledgerProtocol -Name "instrumentation" -Path "ledger.protocol"
    Assert-QualifyingInstrumentationContract -Instrumentation $ledgerInstrumentation -Path "ledger.protocol.instrumentation"

    $ledgerPresentMon = Get-RequiredProperty -InputObject $ledger -Name "presentmon" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "required" -Path "ledger.presentmon") -eq $true) -Message "Runtime ledger does not require PresentMon."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "host_declared_capture_started" -Path "ledger.presentmon") -eq $true) -Message "Runtime did not observe the host PresentMon declaration."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "target_process_id" -Path "ledger.presentmon") -eq $runtimeProcessId) -Message "Ledger PresentMon target PID mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "session_id" -Path "ledger.presentmon") -eq $sessionId) -Message "Ledger PresentMon session mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "tool_version" -Path "ledger.presentmon") -eq $presentMonVersion) -Message "Ledger PresentMon version mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "tool_sha256" -Path "ledger.presentmon") -eq $presentMonSha256) -Message "Ledger PresentMon hash mismatch."
    Assert-Condition -Condition ([uint64](Get-RequiredProperty -InputObject $ledgerPresentMon -Name "last_pre_signal_qpc" -Path "ledger.presentmon") -eq $lastPreSignalQpc) -Message "Ledger PresentMon pre-signal QPC mismatch."
    Assert-Condition -Condition ([uint64](Get-RequiredProperty -InputObject $ledgerPresentMon -Name "measurement_not_before_qpc" -Path "ledger.presentmon") -eq $measurementNotBeforeQpc) -Message "Ledger PresentMon QPC boundary mismatch."
    Assert-Condition -Condition ([uint64](Get-RequiredProperty -InputObject $ledgerPresentMon -Name "qpc_frequency_hz" -Path "ledger.presentmon") -eq $qpcFrequencyHz) -Message "Ledger PresentMon QPC frequency mismatch."
    Assert-Condition -Condition ([int](Get-RequiredProperty -InputObject $ledgerPresentMon -Name "pre_signal_observed_rows" -Path "ledger.presentmon") -eq $preSignalObservedRows) -Message "Ledger PresentMon pre-signal row count mismatch."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "runtime_verified_capture" -Path "ledger.presentmon") -eq $false) -Message "Runtime falsely claims it verified the external capture."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPresentMon -Name "display_metrics_ingested" -Path "ledger.presentmon") -eq $false) -Message "Runtime falsely claims it ingested display metrics."
    $ledgerControllerQpc = Get-RequiredProperty -InputObject $ledger -Name "controller_window_qpc" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerControllerQpc -Name "clock" -Path "ledger.controller_window_qpc") -eq "windows_query_performance_counter") -Message "Runtime controller boundary does not identify the Windows QPC clock."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerControllerQpc -Name "interval" -Path "ledger.controller_window_qpc") -eq "half_open_start_inclusive_end_exclusive") -Message "Runtime controller QPC interval is not half-open."
    [uint64]$controllerWindowStartQpc = Get-RequiredProperty -InputObject $ledgerControllerQpc -Name "start_qpc" -Path "ledger.controller_window_qpc"
    [uint64]$controllerWindowEndQpc = Get-RequiredProperty -InputObject $ledgerControllerQpc -Name "end_qpc" -Path "ledger.controller_window_qpc"
    [uint64]$controllerWindowFrequencyHz = Get-RequiredProperty -InputObject $ledgerControllerQpc -Name "frequency_hz" -Path "ledger.controller_window_qpc"

    $ledgerGovernedScene = Get-RequiredProperty -InputObject $ledger -Name "governed_scene" -Path "ledger"
    foreach ($field in @("qualified_before_measurement", "qualified_for_entire_measurement", "structural_identity_stable", "final_main_world_boundary_validated")) {
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerGovernedScene -Name $field -Path "ledger.governed_scene") -eq $true) -Message "Governed scene failed $field."
    }
    $finalVisibilityAudit = Get-RequiredProperty -InputObject $ledgerGovernedScene -Name "final_visibility_audit" -Path "ledger.governed_scene"
    [uint64]$finalVisibilityAuditTicket = Get-RequiredProperty -InputObject $finalVisibilityAudit -Name "ticket_id" -Path "ledger.governed_scene.final_visibility_audit"
    [uint64]$finalVisibilityAuditCompleted = Get-RequiredProperty -InputObject $finalVisibilityAudit -Name "completed_id" -Path "ledger.governed_scene.final_visibility_audit"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $finalVisibilityAudit -Name "interval_ms" -Path "ledger.governed_scene.final_visibility_audit") -eq 100) -Message "Final vegetation visibility audit interval changed."
    Assert-Condition -Condition ($finalVisibilityAuditTicket -gt 0) -Message "Final vegetation visibility audit ticket is absent."
    Assert-Condition -Condition ($finalVisibilityAuditCompleted -ge $finalVisibilityAuditTicket) -Message "Final vegetation visibility audit receipt does not cover its ticket."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerGovernedScene -Name "water_animation_advanced_each_main_world_update" -Path "ledger.governed_scene") -eq $true) -Message "Water animation did not advance on every measured main-world update."
    $waterAnimationAdvancementUpdates = [int](Get-RequiredProperty -InputObject $ledgerGovernedScene -Name "water_animation_advancement_updates" -Path "ledger.governed_scene")
    Assert-Condition -Condition ($waterAnimationAdvancementUpdates -gt 0) -Message "No water animation advancement was observed during measurement."

    $ledgerPipeline = Get-RequiredProperty -InputObject $ledger -Name "pipeline_warmup" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPipeline -Name "ready_before_measurement" -Path "ledger.pipeline_warmup") -eq $true) -Message "Pipelines were not ready before measurement."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerPipeline -Name "ready_for_entire_measurement" -Path "ledger.pipeline_warmup") -eq $true) -Message "Pipelines were not ready for the whole measurement."
    $ledgerQuality = Get-RequiredProperty -InputObject $ledger -Name "quality" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerQuality -Name "fixed_high_before_measurement" -Path "ledger.quality") -eq $true) -Message "High was not fixed before measurement."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerQuality -Name "fixed_high_for_entire_measurement" -Path "ledger.quality") -eq $true) -Message "High was not fixed for the whole measurement."
    $ledgerScenery = Get-RequiredProperty -InputObject $ledger -Name "scenery" -Path "ledger"
    foreach ($field in @("nine_of_nine_before_measurement", "nine_of_nine_for_entire_measurement", "no_fallback_before_measurement", "no_fallback_for_entire_measurement")) {
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerScenery -Name $field -Path "ledger.scenery") -eq $true) -Message "Ledger scenery failed $field."
    }
    $ledgerGpu = Get-RequiredProperty -InputObject $ledger -Name "gpu" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerGpu -Name "present_before_measurement" -Path "ledger.gpu") -eq $true) -Message "GPU identity was absent before measurement."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerGpu -Name "identity_stable" -Path "ledger.gpu") -eq $true) -Message "GPU identity changed during measurement."

    $movement = Get-RequiredProperty -InputObject $ledger -Name "movement" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "kind" -Path "ledger.movement") -eq "uninterrupted_fixed_tick_gameplay_controller") -Message "Movement is not the uninterrupted gameplay controller."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "forward_ticks" -Path "ledger.movement") -eq 900) -Message "Forward controller window is not 900 ticks."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "reverse_ticks" -Path "ledger.movement") -eq 900) -Message "Reverse controller window is not 900 ticks."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "total_fixed_ticks" -Path "ledger.movement") -eq 1800) -Message "Controller window is not exactly 1800 ticks."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "nominal_duration_seconds" -Path "ledger.movement") -eq 30) -Message "Nominal controller duration is not 30 seconds."
    $observedWallSeconds = [double](Get-RequiredProperty -InputObject $movement -Name "observed_wall_duration_seconds" -Path "ledger.movement")
    Assert-Condition -Condition ([double]::IsFinite($observedWallSeconds) -and $observedWallSeconds -ge 29.5 -and $observedWallSeconds -le 30.5) -Message "Observed controller wall duration is outside 29.5..30.5 seconds."
    $verifiedControllerQpcWindow = Get-VerifiedRuntimeControllerQpcWindow `
        -HostLowerBoundQpc $measurementNotBeforeQpc `
        -StartQpc $controllerWindowStartQpc `
        -EndQpc $controllerWindowEndQpc `
        -RuntimeFrequencyHz $controllerWindowFrequencyHz `
        -HostFrequencyHz $qpcFrequencyHz
    Assert-Condition -Condition ($verifiedControllerQpcWindow.DurationSeconds -ge 29.5 -and $verifiedControllerQpcWindow.DurationSeconds -le 30.5) -Message "Runtime controller QPC duration is outside 29.5..30.5 seconds."
    $monotonicDurationDeltaMilliseconds = [Math]::Abs($verifiedControllerQpcWindow.DurationSeconds - $observedWallSeconds) * 1000.0
    $nativeWindowContinuity = Get-Gate1NativeWindowContinuity `
        -Samples @($nativeWindowSamples) `
        -StartQpc $controllerWindowStartQpc `
        -EndQpc $controllerWindowEndQpc `
        -FrequencyHz $controllerWindowFrequencyHz
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "forward_distance_m" -Path "ledger.movement") -ge 60.0) -Message "Forward controller leg is shorter than 60 m."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "reverse_distance_m" -Path "ledger.movement") -ge 60.0) -Message "Reverse controller leg is shorter than 60 m."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "return_distance_m" -Path "ledger.movement") -le 5.0) -Message "Controller did not return within 5 m."
    $startPosition = @(Get-RequiredProperty -InputObject $movement -Name "start_position_m" -Path "ledger.movement")
    $forwardEndpoint = @(Get-RequiredProperty -InputObject $movement -Name "forward_endpoint_m" -Path "ledger.movement")
    $endPosition = @(Get-RequiredProperty -InputObject $movement -Name "end_position_m" -Path "ledger.movement")
    Assert-Condition -Condition ($startPosition.Count -eq 3 -and $forwardEndpoint.Count -eq 3 -and $endPosition.Count -eq 3) -Message "Movement positions are incomplete."
    Assert-Condition -Condition ($startPosition[2] -gt 0.0 -and $forwardEndpoint[2] -lt 0.0 -and $endPosition[2] -gt 0.0) -Message "Controller did not cross and recross the central seam."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "grounded_ticks" -Path "ledger.movement") -eq 1800) -Message "Controller was not grounded for all 1800 ticks."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "collision_ticks" -Path "ledger.movement") -gt 0) -Message "Controller never observed a non-empty KCC collision list."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "maximum_ungrounded_run" -Path "ledger.movement") -le 4) -Message "Controller grounding gap exceeded four ticks."
    $profileHorizontalStepLimit = [double](Get-RequiredProperty -InputObject $movement -Name "profile_horizontal_step_limit_m" -Path "ledger.movement")
    $maximumHorizontalStep = [double](Get-RequiredProperty -InputObject $movement -Name "maximum_horizontal_step_m" -Path "ledger.movement")
    $maximumControllerTranslationError = [double](Get-RequiredProperty -InputObject $movement -Name "maximum_controller_translation_error_m" -Path "ledger.movement")
    Assert-Condition -Condition ([double]::IsFinite($profileHorizontalStepLimit) -and $profileHorizontalStepLimit -gt 0.0 -and $profileHorizontalStepLimit -lt 0.5) -Message "Profile-derived horizontal step limit is invalid."
    Assert-Condition -Condition ([double]::IsFinite($maximumHorizontalStep) -and $maximumHorizontalStep -le $profileHorizontalStepLimit) -Message "Controller exceeded its profile-derived per-tick movement bound."
    Assert-Condition -Condition ([double]::IsFinite($maximumControllerTranslationError) -and $maximumControllerTranslationError -le 0.001) -Message "Observed player movement diverged from the controller/KCC effective translation."
    $firstMainWorldFrame = [uint32](Get-RequiredProperty -InputObject $movement -Name "first_main_world_frame" -Path "ledger.movement")
    $lastMainWorldFrame = [uint32](Get-RequiredProperty -InputObject $movement -Name "last_main_world_frame" -Path "ledger.movement")
    $mainWorldFramesSpanned = [uint32](Get-RequiredProperty -InputObject $movement -Name "main_world_frames_spanned" -Path "ledger.movement")
    Assert-Condition -Condition ($lastMainWorldFrame -ge $firstMainWorldFrame -and $mainWorldFramesSpanned -eq ($lastMainWorldFrame - $firstMainWorldFrame + 1)) -Message "Controller main-world frame span is inconsistent."
    $mainWorldInvariantChecks = [int](Get-RequiredProperty -InputObject $movement -Name "main_world_invariant_checks" -Path "ledger.movement")
    Assert-Condition -Condition ($mainWorldInvariantChecks -gt 0) -Message "No main-world invariant was validated."
    Assert-Condition -Condition ($waterAnimationAdvancementUpdates -eq $mainWorldInvariantChecks) -Message "Water animation advancement count does not match validated main-world updates."
    $firstFixedTick = [uint64](Get-RequiredProperty -InputObject $movement -Name "first_fixed_tick" -Path "ledger.movement")
    $midpointFixedTick = [uint64](Get-RequiredProperty -InputObject $movement -Name "midpoint_fixed_tick" -Path "ledger.movement")
    $lastFixedTick = [uint64](Get-RequiredProperty -InputObject $movement -Name "last_fixed_tick" -Path "ledger.movement")
    Assert-Condition -Condition ($firstFixedTick -le $midpointFixedTick -and $midpointFixedTick -le $lastFixedTick) -Message "Movement fixed-tick boundaries are not monotonic."
    Assert-Condition -Condition ($midpointFixedTick - $firstFixedTick -eq 899 -and $lastFixedTick - $midpointFixedTick -eq 900) -Message "Movement fixed-tick boundaries are not exactly consecutive 900+900 samples."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "stop_wait_ticks" -Path "ledger.movement") -le 45) -Message "Controller stop exceeded 45 ticks."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "stop_stable_ticks" -Path "ledger.movement") -gt 0) -Message "Controller stop never stabilized."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "camera_marker_verified" -Path "ledger.movement") -eq $true) -Message "ForgePlayerCamera was not verified."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "rapier_bindings_stable" -Path "ledger.movement") -eq $true) -Message "Rapier bindings changed."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "virtual_time_unpaused" -Path "ledger.movement") -eq $true) -Message "Virtual time was paused."
    $virtualTimeScale = [double](Get-RequiredProperty -InputObject $movement -Name "virtual_time_scale" -Path "ledger.movement")
    Assert-Condition -Condition ([Math]::Abs($virtualTimeScale - 1.0) -le 0.000001) -Message "Virtual time scale was not exactly 1.0."
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $movement -Name "screenshots_observed" -Path "ledger.movement") -eq $false) -Message "Screenshots were observed during the real-time controller window."
    $binding = Get-RequiredProperty -InputObject $movement -Name "binding" -Path "ledger.movement"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $binding -Name "backend_identity_stable" -Path "ledger.movement.binding") -eq $true) -Message "Controller backend identity changed."
    foreach ($field in @("player_entity", "camera_entity", "rapier_context_entity")) {
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string](Get-RequiredProperty -InputObject $binding -Name $field -Path "ledger.movement.binding"))) -Message "Movement binding $field is absent."
    }

    $ledgerQualification = Get-RequiredProperty -InputObject $ledger -Name "qualification" -Path "ledger"
    Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerQualification -Name "real_gameplay_controller_exercised" -Path "ledger.qualification") -eq $true) -Message "Runtime did not exercise the real gameplay controller."
    foreach ($field in @("display_stutter_qualified", "presentmon_analysis_completed", "human_review_performed", "gate1_route_qualified", "gate1_qualified")) {
        Assert-Condition -Condition ((Get-RequiredProperty -InputObject $ledgerQualification -Name $field -Path "ledger.qualification") -eq $false) -Message "Runtime ledger overstates $field before host validation."
    }

    Assert-Condition -Condition ([IO.File]::Exists($csvPath)) -Message "PresentMon CSV is absent."
    $finalHeader = Get-FirstLineShared -Path $csvPath
    Assert-Condition -Condition ([string]::Equals($finalHeader, $expectedPresentMonHeader, [StringComparison]::Ordinal)) -Message "Final PresentMon CSV header changed."
    $csvRows = @(Import-Csv -LiteralPath $csvPath)
    Assert-Condition -Condition ($csvRows.Count -ge 120) -Message "PresentMon CSV contains fewer than 120 rows."
    Assert-Condition -Condition ($presentMonStreamPartition.CsvRows -eq $csvRows.Count -and $presentMonStreamPartition.CsvLines -eq $csvRows.Count + 1) -Message "PresentMon raw-stream partition counts do not match the canonical CSV."
    Assert-Condition -Condition ($presentMonStreamPartition.ConsoleLines -eq $presentMonLogAssessment.EtwStatusCount) -Message "PresentMon raw-stream console partition does not match the validated ETW status log."
    $parsedRows = [Collections.Generic.List[object]]::new()
    $swapChains = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $presentModes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $presentRuntimes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $allowsTearingValues = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $syncIntervals = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $presentFlagsValues = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $notDisplayedCount = 0
    $previousQpc = $null
    for ($index = 0; $index -lt $csvRows.Count; $index++) {
        $row = $csvRows[$index]
        $csvRowNumber = $index + 2
        Assert-Condition -Condition (@($row.PSObject.Properties).Count -eq $expectedPresentMonColumns.Count) -Message "CSV row $csvRowNumber does not have the exact v2/ETW column count."
        $application = [string](Get-RequiredProperty -InputObject $row -Name "Application" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ([string]::Equals($application, [IO.Path]::GetFileName($runtimePath), [StringComparison]::OrdinalIgnoreCase)) -Message "CSV row $csvRowNumber names unexpected application '$application'."
        $processIdText = [string](Get-RequiredProperty -InputObject $row -Name "ProcessID" -Path "csv[$csvRowNumber]")
        $rowProcessId = 0
        Assert-Condition -Condition ([int]::TryParse($processIdText, [Globalization.NumberStyles]::None, [Globalization.CultureInfo]::InvariantCulture, [ref]$rowProcessId) -and $rowProcessId -eq $runtimeProcessId) -Message "CSV row $csvRowNumber PID mismatch."
        $swapChain = [string](Get-RequiredProperty -InputObject $row -Name "SwapChainAddress" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($swapChain -match '^0x[0-9A-Fa-f]+$') -Message "CSV row $csvRowNumber has invalid SwapChainAddress '$swapChain'."
        [void]$swapChains.Add($swapChain)
        $presentRuntime = [string](Get-RequiredProperty -InputObject $row -Name "PresentRuntime" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($presentRuntime -eq $expectedPresentRuntime) -Message "CSV row $csvRowNumber is not the governed Windows presentation runtime '$expectedPresentRuntime': '$presentRuntime'."
        [void]$presentRuntimes.Add($presentRuntime)
        $presentMode = [string](Get-RequiredProperty -InputObject $row -Name "PresentMode" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($presentMode) -and $presentMode -ne "NA") -Message "CSV row $csvRowNumber has no PresentMode."
        [void]$presentModes.Add($presentMode)
        $allowsTearing = [string](Get-RequiredProperty -InputObject $row -Name "AllowsTearing" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($allowsTearing -in @("0", "1")) -Message "CSV row $csvRowNumber has invalid AllowsTearing '$allowsTearing'."
        [void]$allowsTearingValues.Add($allowsTearing)
        $syncInterval = [string](Get-RequiredProperty -InputObject $row -Name "SyncInterval" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($syncInterval -match '^\d+$') -Message "CSV row $csvRowNumber has invalid SyncInterval."
        [void]$syncIntervals.Add($syncInterval)
        $presentFlags = [string](Get-RequiredProperty -InputObject $row -Name "PresentFlags" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($presentFlags -match '^(?:0x[0-9A-Fa-f]+|\d+)$') -Message "CSV row $csvRowNumber has invalid PresentFlags."
        [void]$presentFlagsValues.Add($presentFlags)
        $qpcText = [string](Get-RequiredProperty -InputObject $row -Name "CPUStartQPC" -Path "csv[$csvRowNumber]")
        Assert-Condition -Condition ($qpcText -match '^\d+$') -Message "CSV row $csvRowNumber has invalid CPUStartQPC '$qpcText'."
        $qpc = [decimal]::Parse($qpcText, [Globalization.CultureInfo]::InvariantCulture)
        if ($null -ne $previousQpc) {
            Assert-Condition -Condition ($qpc -gt $previousQpc) -Message "PresentMon CPUStartQPC is not strictly increasing at CSV row $csvRowNumber."
        }
        $previousQpc = $qpc
        [void](ConvertTo-PresentMonMetric -Value ([string]$row.FrameTime) -Field "csv[$csvRowNumber].FrameTime" -Minimum 0.0 -StrictlyGreaterThanMinimum)
        [void](ConvertTo-PresentMonMetric -Value ([string]$row.CPUBusy) -Field "csv[$csvRowNumber].CPUBusy" -Minimum 0.0)
        [void](ConvertTo-PresentMonMetric -Value ([string]$row.CPUWait) -Field "csv[$csvRowNumber].CPUWait" -Minimum 0.0)
        foreach ($field in @("GPULatency", "GPUTime", "GPUBusy", "GPUWait", "AnimationError", "AnimationTime", "MsFlipDelay", "AllInputToPhotonLatency", "ClickToPhotonLatency")) {
            [void](ConvertTo-PresentMonMetric -Value ([string]$row.$field) -Field "csv[$csvRowNumber].$field" -AllowNa)
        }
        $displayLatencyText = [string](Get-RequiredProperty -InputObject $row -Name "DisplayLatency" -Path "csv[$csvRowNumber]")
        $displayedText = [string](Get-RequiredProperty -InputObject $row -Name "DisplayedTime" -Path "csv[$csvRowNumber]")
        $displayLatency = $null
        $displayedTime = $null
        if ([string]::Equals($displayedText, "NA", [StringComparison]::Ordinal)) {
            Assert-Condition -Condition ([string]::Equals($displayLatencyText, "NA", [StringComparison]::Ordinal)) -Message "CSV row $csvRowNumber has mismatched DisplayLatency/DisplayedTime NA state."
            $notDisplayedCount++
        }
        else {
            $displayLatency = ConvertTo-InvariantDouble -Value $displayLatencyText -Field "csv[$csvRowNumber].DisplayLatency" -Minimum 0.0
            $displayedTime = ConvertTo-InvariantDouble -Value $displayedText -Field "csv[$csvRowNumber].DisplayedTime" -Minimum 0.0 -StrictlyGreaterThanMinimum
        }
        $etwFill = ConvertTo-InvariantDouble -Value ([string]$row.EtwBufferFillPct -replace '%$', '') -Field "csv[$csvRowNumber].EtwBufferFillPct" -Minimum 0.0
        Assert-Condition -Condition ($etwFill -lt 100.0) -Message "CSV row $csvRowNumber ETW buffer fill reached 100 percent."
        foreach ($field in @("EtwBuffersInUse", "EtwTotalBuffers", "EtwEventsLost", "EtwBuffersLost")) {
            Assert-Condition -Condition ([string]$row.$field -match '^\d+$') -Message "CSV row $csvRowNumber has invalid $field."
        }
        Assert-Condition -Condition ([uint64]$row.EtwTotalBuffers -gt 0) -Message "CSV row $csvRowNumber reports no ETW buffers."
        Assert-Condition -Condition ([uint64]$row.EtwBuffersInUse -le [uint64]$row.EtwTotalBuffers) -Message "CSV row $csvRowNumber reports more ETW buffers in use than total."
        Assert-Condition -Condition ([uint64]$row.EtwEventsLost -eq 0 -and [uint64]$row.EtwBuffersLost -eq 0) -Message "CSV row $csvRowNumber reports lost ETW events or buffers."
        $parsedRows.Add([pscustomobject]@{
                CsvRow = $csvRowNumber
                Qpc = $qpc
                DisplayLatencyMs = $displayLatency
                DisplayedTimeMs = $displayedTime
            })
    }
    Assert-Condition -Condition ($swapChains.Count -eq 1) -Message "PresentMon recorded $($swapChains.Count) swap chains; one exact display surface is required."
    Assert-Condition -Condition ($presentRuntimes.Count -eq 1) -Message "PresentRuntime changed during the capture."
    Assert-Condition -Condition ($presentModes.Count -gt 0 -and $allowsTearingValues.Count -gt 0 -and $syncIntervals.Count -gt 0 -and $presentFlagsValues.Count -gt 0) -Message "PresentMon did not expose a complete presentation signature."
    $presentationSignatureStable = ($presentModes.Count -eq 1 -and $allowsTearingValues.Count -eq 1 -and $syncIntervals.Count -eq 1 -and $presentFlagsValues.Count -eq 1)
    if ($presentationContract.Name -eq "AutoNoVsync") {
        Assert-Condition -Condition ($presentModes.Count -eq 1) -Message "PresentMode changed during the AutoNoVsync capture."
        Assert-Condition -Condition ($allowsTearingValues.Count -eq 1) -Message "AllowsTearing changed during the AutoNoVsync capture."
        Assert-Condition -Condition ($syncIntervals.Count -eq 1) -Message "SyncInterval changed during the AutoNoVsync capture."
        Assert-Condition -Condition ($presentFlagsValues.Count -eq 1) -Message "PresentFlags changed during the AutoNoVsync capture."
        Assert-Condition -Condition (@($syncIntervals)[0] -eq $presentationContract.ExpectedSyncInterval) -Message "PresentMon observed a non-zero SyncInterval despite the governed AutoNoVsync protocol."
    }

    [decimal]$measurementStartQpc = $verifiedControllerQpcWindow.StartQpc
    [decimal]$measurementEndQpc = $verifiedControllerQpcWindow.EndQpc
    $presentMonControllerProjection = Get-PresentMonControllerWindowProjection `
            -Rows @($parsedRows) `
            -StartQpc $controllerWindowStartQpc `
            -EndQpc $controllerWindowEndQpc `
            -FrequencyHz $controllerWindowFrequencyHz
    $measurementRows = @($presentMonControllerProjection.DisplayedOverlapRows)
    $measurementNotDisplayedRows = @($presentMonControllerProjection.NotDisplayedCpuStartRows)
    $measurementEnvelopeRowCount = $measurementRows.Count + $measurementNotDisplayedRows.Count
    Assert-Condition -Condition ($measurementEnvelopeRowCount -ge 120) -Message "The runtime-window PresentMon projection contains fewer than 120 rows."
    Assert-Condition -Condition ($measurementRows.Count -ge 120) -Message "The runtime-window PresentMon projection contains fewer than 120 displayed rows."
    Assert-PresentMonControllerWindowCoverage -Projection $presentMonControllerProjection

    $displayedTimes = [double[]]@($measurementRows | ForEach-Object { [double]$_.DisplayedTimeMs })
    $sortedDisplayedTimes = [double[]]@($displayedTimes | Sort-Object)
    $meanMilliseconds = [double](($displayedTimes | Measure-Object -Average).Average)
    $meanDisplayFps = 1000.0 / $meanMilliseconds
    $p95Milliseconds = Get-NearestRankPercentile -SortedValues $sortedDisplayedTimes -Quantile 0.95
    $p99Milliseconds = Get-NearestRankPercentile -SortedValues $sortedDisplayedTimes -Quantile 0.99
    $slowestOnePercentCount = [Math]::Max(1, [int][Math]::Ceiling($displayedTimes.Count / 100.0))
    $slowestOnePercent = [double[]]@($sortedDisplayedTimes | Sort-Object -Descending | Select-Object -First $slowestOnePercentCount)
    $slowestOnePercentMeanMilliseconds = [double](($slowestOnePercent | Measure-Object -Average).Average)
    $onePercentLowFps = 1000.0 / $slowestOnePercentMeanMilliseconds
    $over16Count = @($displayedTimes | Where-Object { $_ -gt 16.7 }).Count
    $over33Count = @($displayedTimes | Where-Object { $_ -gt 33.3 }).Count
    $over16Sequences = @(Get-ThresholdSequences -Rows @($measurementRows) -ThresholdMilliseconds 16.7)
    $over33Sequences = @(Get-ThresholdSequences -Rows @($measurementRows) -ThresholdMilliseconds 33.3)
    $maxConsecutiveOver16 = if ($over16Sequences.Count -eq 0) { 0 } else { [int](($over16Sequences | Measure-Object -Property length -Maximum).Maximum) }
    $maxConsecutiveOver33 = if ($over33Sequences.Count -eq 0) { 0 } else { [int](($over33Sequences | Measure-Object -Property length -Maximum).Maximum) }
    $displayCoveragePassed = $presentMonControllerProjection.CoversStart -and $presentMonControllerProjection.CoversEnd -and [decimal]$presentMonControllerProjection.MaximumGapQpc -le [decimal]$presentMonControllerProjection.QuantizationToleranceQpc
    $displayStutterThresholdAssessment = Get-Gate1DisplayedStutterThresholdAssessment `
        -DisplayCoveragePassed $displayCoveragePassed `
        -NotDisplayedRows $measurementNotDisplayedRows.Count `
        -MeanDisplayFps $meanDisplayFps `
        -P95Milliseconds $p95Milliseconds `
        -OnePercentLowFps $onePercentLowFps `
        -Over33Count $over33Count `
        -MaximumConsecutiveOver16 $maxConsecutiveOver16
    $displayThresholdsPassed = $displayStutterThresholdAssessment.ThresholdsPassed
    $boundedThresholdsPassed = $displayThresholdsPassed -and $nativeWindowContinuity.StatePassed -and $presentationSignatureStable
    $validationStatus = if ($presentationContract.Name -eq "FifoDiagnostic") {
        if ($boundedThresholdsPassed) { "diagnostic_thresholds_passed" } else { "diagnostic_thresholds_failed" }
    }
    elseif ($boundedThresholdsPassed) {
        "runtime_qpc_display_overlap_thresholds_passed_pending_gate1_qualification"
    }
    else {
        "runtime_qpc_display_overlap_thresholds_failed"
    }

    $validation = [ordered]@{
        schema = "forge.gate1-gameplay-stutter-validation"
        schema_version = 8
        status = $validationStatus
        presentation_route = $presentationContract.PresentationRoute
        requested_present_mode = $presentationContract.RequestedPresentMode
        vsync_requested = $presentationContract.VsyncRequested
        qualification_eligible = $presentationContract.QualificationEligible
        observation_scope = "bevy_window_request_not_resolved_surface"
        repository_commit = $gitCommit
        repository_tree = $gitTree
        runtime = [ordered]@{
            process_id = $runtimeProcessId
            executable_sha256 = $runtimeSha256
            governed_build_completed_at_utc = $runtimeBuildCompletedAtUtc
            governed_build_cargo_version = $cargoVersion
            governed_build_target_directory = $cargoTargetPath
            governed_build_working_directory = $repositoryPath
            governed_build_arguments = @("build", "-p", "forge_runtime", "--release", "--locked", "-j", "1")
            bevy_asset_root = $repositoryPath
            asset_directory = $assetDirectory
            assets_referenced_in_place = $true
            assets_copied_to_release_directory = $false
            visible_window_requested = $true
            visible_window_observed_before_signal = $nativeWindowObservation.Observed
            native_window = [ordered]@{
                handle = $nativeWindowObservation.Handle
                owner_process_id = $nativeWindowObservation.OwnerProcessId
                ws_visible_observed = $nativeWindowObservation.Visible
                minimized_observed = $nativeWindowObservation.Minimized
                outer_rect_reported_px = @($nativeWindowObservation.OuterWidthReportedPx, $nativeWindowObservation.OuterHeightReportedPx)
                minimum_outer_rect_reported_px = @(1920, 1080)
                window_wait_elapsed_ms = $nativeWindowObservation.ObservedAfterWaitStartMilliseconds
            }
            foreground_attempt_before_signal = [ordered]@{
                initial_foreground = $foregroundAttempt.InitialForeground
                app_activate_attempted = $foregroundAttempt.AppActivateAttempted
                app_activate_returned = $foregroundAttempt.AppActivateReturned
                app_activate_error = $foregroundAttempt.AppActivateError
                set_foreground_window_attempted = $foregroundAttempt.SetForegroundWindowAttempted
                set_foreground_window_returned = $foregroundAttempt.SetForegroundWindowReturned
                attached_thread_fallback_attempted = $foregroundAttempt.ForcedActivationAttempted
                attached_thread_fallback_returned = $foregroundAttempt.ForcedActivationReturned
                foreground_observed = $foregroundAttempt.ForegroundObserved
                elapsed_ms = $foregroundAttempt.ElapsedMilliseconds
            }
            controller_window_state_sampling = [ordered]@{
                interval_ms = $nativeWindowContinuity.SamplingIntervalMilliseconds
                total_samples = $nativeWindowContinuity.TotalSamples
                controller_envelope_samples = $nativeWindowContinuity.ControllerEnvelopeSamples
                covers_controller_start = $nativeWindowContinuity.CoversControllerStart
                covers_controller_end = $nativeWindowContinuity.CoversControllerEnd
                first_envelope_sample_qpc = $nativeWindowContinuity.FirstEnvelopeSampleQpc
                last_envelope_sample_qpc = $nativeWindowContinuity.LastEnvelopeSampleQpc
                maximum_sampling_gap_qpc = $nativeWindowContinuity.MaximumSamplingGapQpc
                maximum_sampling_gap_ms = $nativeWindowContinuity.MaximumSamplingGapMilliseconds
                maximum_sampling_gap_threshold_ms = $nativeWindowContinuity.MaximumSamplingGapThresholdMilliseconds
                sampling_gap_passed = $nativeWindowContinuity.SamplingGapPassed
                handle_mismatch_samples = $nativeWindowContinuity.HandleMismatchSamples
                owner_mismatch_samples = $nativeWindowContinuity.OwnerMismatchSamples
                not_visible_samples = $nativeWindowContinuity.NotVisibleSamples
                minimized_samples = $nativeWindowContinuity.MinimizedSamples
                not_foreground_samples = $nativeWindowContinuity.NotForegroundSamples
                first_handle_mismatch_qpc = $nativeWindowContinuity.FirstHandleMismatchQpc
                first_owner_mismatch_qpc = $nativeWindowContinuity.FirstOwnerMismatchQpc
                first_not_visible_qpc = $nativeWindowContinuity.FirstNotVisibleQpc
                first_minimized_qpc = $nativeWindowContinuity.FirstMinimizedQpc
                first_not_foreground_qpc = $nativeWindowContinuity.FirstNotForegroundQpc
                handle_stable = $nativeWindowContinuity.HandleStable
                owner_stable = $nativeWindowContinuity.OwnerStable
                visible_for_all_samples = $nativeWindowContinuity.VisibleForAllSamples
                never_minimized = $nativeWindowContinuity.NeverMinimized
                foreground_for_all_samples = $nativeWindowContinuity.ForegroundForAllSamples
                state_passed = $nativeWindowContinuity.StatePassed
            }
            release_profile_declared_by_runtime = $true
            controller_wall_duration_seconds = $observedWallSeconds
            instrumentation = [ordered]@{
                bevy_render_diagnostics_plugin_enabled = [bool](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "bevy_render_diagnostics_plugin_enabled" -Path "ledger.protocol.instrumentation")
                bevy_render_diagnostics_resolve_encoder_enabled = [bool](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "bevy_render_diagnostics_resolve_encoder_enabled" -Path "ledger.protocol.instrumentation")
                bevy_render_pass_cpu_timing_status = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "bevy_render_pass_cpu_timing_status" -Path "ledger.protocol.instrumentation")
                bevy_render_pass_gpu_timing_status = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "bevy_render_pass_gpu_timing_status" -Path "ledger.protocol.instrumentation")
                bevy_render_pipeline_statistics_status = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "bevy_render_pipeline_statistics_status" -Path "ledger.protocol.instrumentation")
                runtime_presented_frame_count_status = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "runtime_presented_frame_count_status" -Path "ledger.protocol.instrumentation")
                application_update_timing_status = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "application_update_timing_status" -Path "ledger.protocol.instrumentation")
                display_timing_source = [string](Get-RequiredProperty -InputObject $ledgerInstrumentation -Name "display_timing_source" -Path "ledger.protocol.instrumentation")
            }
        }
        presentmon = [ordered]@{
            version = $presentMonVersion
            version_identity_source = "official_release_asset_bytes_sha256_and_authenticode"
            windows_version_resource_present = $presentMonVersionResourcePresent
            bytes = $presentMonBytes
            sha256 = $presentMonSha256
            authenticode_status = [string]$presentMonSignature.Status
            signer = $presentMonSigner
            session_id = $sessionId
            session_guard = [ordered]@{
                mutex_name = "Global\OpenRPGForge-Gate1-PresentMon-Guard"
                mutex_scope = "machine_wide_windows_kernel_namespace"
                recognized_session_prefixes = @(
                    "OpenRPGForge-Gate1-Stutter-<32-hex>"
                    "OpenRPGForge-Gate1-Diagnostic-<32-hex>"
                )
                initial_preflight_no_active_gate1_sessions = $true
                pre_start_no_active_gate1_sessions = $true
                exact_owned_session_postflight_absent = $true
                unrelated_session_termination_permitted = $false
            }
            target_process_id = $runtimeProcessId
            session_stop_mode = "terminate_existing_session"
            session_stop_exit_code = $presentMonStopExitCode
            session_stop_timeout_seconds = $presentMonExitTimeoutSeconds
            session_stop_timed_out = $presentMonStopResult.TimedOut
            session_stop_termination_confirmed = $presentMonStopResult.TerminationConfirmed
            session_stop_stdout_drained = $presentMonStopResult.StdoutDrained
            session_stop_stderr_drained = $presentMonStopResult.StderrDrained
            output_transport = "stdout_stream"
            raw_stdout_stream_csv_rows = $presentMonStreamPartition.CsvRows
            raw_stdout_stream_console_lines = $presentMonStreamPartition.ConsoleLines
            csv_schema = "presentmon_v2_qpc_with_etw_status"
            csv_columns = $expectedPresentMonColumns
            raw_csv_rows = $parsedRows.Count
            raw_displayed_rows = $parsedRows.Count - $notDisplayedCount
            raw_not_displayed_rows = $notDisplayedCount
            last_pre_signal_qpc = $lastPreSignalQpc
            measurement_not_before_qpc = $measurementNotBeforeQpc
            signal_published_qpc = $signalPublishedQpc
            qpc_frequency_hz = $qpcFrequencyHz
            runtime_controller_window_start_qpc = [uint64]$measurementStartQpc
            runtime_controller_window_end_qpc = [uint64]$measurementEndQpc
            runtime_controller_window_interval = "half_open_start_inclusive_end_exclusive"
            runtime_controller_window_qpc_frequency_hz = [uint64]$verifiedControllerQpcWindow.FrequencyHz
            runtime_controller_window_duration_seconds = $verifiedControllerQpcWindow.DurationSeconds
            monotonic_duration_delta_ms = $monotonicDurationDeltaMilliseconds
            pre_signal_observed_rows = $preSignalObservedRows
            measurement_selection = $presentMonControllerProjection.DisplayTimingSource
            measurement_interval = $presentMonControllerProjection.Interval
            measurement_envelope_rows = $measurementEnvelopeRowCount
            measurement_displayed_rows = $measurementRows.Count
            measurement_not_displayed_rows = $measurementNotDisplayedRows.Count
            display_residency_covers_start = $presentMonControllerProjection.CoversStart
            display_residency_covers_end = $presentMonControllerProjection.CoversEnd
            display_residency_maximum_gap_qpc = $presentMonControllerProjection.MaximumGapQpc
            display_residency_total_gap_qpc_diagnostic = $presentMonControllerProjection.TotalGapQpc
            display_residency_quantization_tolerance_qpc = $presentMonControllerProjection.QuantizationToleranceQpc
            exact_display_qpc_bounds_observed = $false
            swap_chain = @($swapChains)[0]
            present_runtime = @($presentRuntimes)[0]
            present_mode = if ($presentModes.Count -eq 1) { @($presentModes)[0] } else { $null }
            present_mode_values = @($presentModes)
            allows_tearing_values = @($allowsTearingValues)
            sync_interval_values = @($syncIntervals)
            present_flags_values = @($presentFlagsValues)
            presentation_signature_stable = $presentationSignatureStable
            etw_events_lost = 0
            etw_buffers_lost = 0
            stdout_etw_status_lines = $presentMonLogAssessment.EtwStatusCount
            allowed_non_admin_warning_count = $presentMonLogAssessment.AllowedNonAdminWarningCount
        }
        display_metrics = [ordered]@{
            source_column = "DisplayedTime"
            percentile_method = "nearest_rank"
            one_percent_low_method = "1000_divided_by_mean_of_slowest_ceil_one_percent_displayed_times"
            sample_count = $displayedTimes.Count
            mean_ms = $meanMilliseconds
            mean_display_fps = $meanDisplayFps
            p95_ms = $p95Milliseconds
            p99_ms = $p99Milliseconds
            slowest_one_percent_count = $slowestOnePercentCount
            slowest_one_percent_mean_ms = $slowestOnePercentMeanMilliseconds
            one_percent_low_fps = $onePercentLowFps
            over_16_7_ms_count = $over16Count
            over_33_3_ms_count = $over33Count
            over_16_7_ms_sequences = $over16Sequences
            over_33_3_ms_sequences = $over33Sequences
            maximum_consecutive_over_16_7_ms = $maxConsecutiveOver16
            maximum_consecutive_over_33_3_ms = $maxConsecutiveOver33
        }
        checks = [ordered]@{
            clean_worktree_before_and_after = $true
            exact_commit_embedded = $true
            runtime_rebuilt_by_host_after_clean_check = $true
            governed_asset_root_is_clean_repository = $true
            release_asset_copy_absent = $true
            runtime_visible_window_observed_before_signal = $nativeWindowObservation.Observed
            runtime_foreground_requested_before_signal = $true
            runtime_foreground_observed_before_signal = $foregroundAttempt.ForegroundObserved
            runtime_window_state_sampling_covers_controller_start = $nativeWindowContinuity.CoversControllerStart
            runtime_window_state_sampling_covers_controller_end = $nativeWindowContinuity.CoversControllerEnd
            runtime_window_maximum_sampling_gap_at_most_250_ms = $nativeWindowContinuity.SamplingGapPassed
            runtime_window_handle_stable_for_all_samples = $nativeWindowContinuity.HandleStable
            runtime_window_owner_stable_for_all_samples = $nativeWindowContinuity.OwnerStable
            runtime_window_visible_for_all_samples = $nativeWindowContinuity.VisibleForAllSamples
            runtime_window_never_minimized = $nativeWindowContinuity.NeverMinimized
            runtime_window_foreground_for_all_samples = $nativeWindowContinuity.ForegroundForAllSamples
            runtime_window_state_passed = $nativeWindowContinuity.StatePassed
            release_1920x1080_high_no_vsync = ($presentationContract.Name -eq "AutoNoVsync")
            release_1920x1080_high_presentation_route_matches_request = $true
            presentation_route_qualification_eligible = $presentationContract.QualificationEligible
            fifo_diagnostic_marker_verified = (-not $presentationContract.MarkerRequired -or (Test-Path -LiteralPath $fifoDiagnosticMarkerPath -PathType Leaf))
            bevy_render_diagnostics_disabled_for_presentmon = $true
            real_gameplay_controller_exercised = $true
            uninterrupted_1800_fixed_ticks = $true
            governed_scene_qualified_and_stable = $true
            presentmon_pid_exact = $true
            presentmon_session_guard_self_test_passed = $true
            presentmon_initial_preflight_no_active_gate1_sessions = $true
            presentmon_pre_start_no_active_gate1_sessions = $true
            presentmon_exact_owned_session_postflight_absent = $true
            presentmon_unrelated_session_termination_permitted = $false
            presentmon_session_stopped_gracefully = (-not $presentMonStopResult.TimedOut -and $presentMonStopResult.TerminationConfirmed -and $presentMonStopResult.StdoutDrained -and $presentMonStopResult.StderrDrained -and $presentMonStopExitCode -eq 0)
            presentmon_stdout_stream_observed_live = $true
            presentmon_raw_stdout_partitioned = $true
            presentmon_v2_qpc_header_exact = $true
            presentmon_qpc_boundary_observed_before_signal = $true
            exact_runtime_qpc_bounds_observed = $true
            runtime_qpc_frequency_matches_host = $true
            runtime_qpc_duration_in_29_5_to_30_5_seconds = $true
            monotonic_duration_delta_recorded = $true
            presentmon_display_intervals_derived_from_latency = $true
            exact_display_qpc_bounds_observed = $false
            presentmon_single_stable_swapchain_epoch = $true
            presentmon_presentation_signature_stable = $presentationSignatureStable
            presentmon_etw_loss_zero = $true
            display_residency_covers_runtime_start = $presentMonControllerProjection.CoversStart
            display_residency_covers_runtime_end = $presentMonControllerProjection.CoversEnd
            display_residency_maximum_gap_within_quantization = ([decimal]$presentMonControllerProjection.MaximumGapQpc -le [decimal]$presentMonControllerProjection.QuantizationToleranceQpc)
            bounded_sample_has_no_not_displayed_frames = $displayStutterThresholdAssessment.HasNoNotDisplayedRows
            not_displayed_frames_are_diagnostic_when_display_residency_is_continuous = $displayStutterThresholdAssessment.NotDisplayedRowsAreDiagnostic
            display_stutter_metric_thresholds_passed = $displayStutterThresholdAssessment.MetricThresholdsPassed
            display_stutter_threshold_contract_self_test_passed = $true
            runtime_warning_error_panic_entries = 0
            presentmon_capture_disallowed_log_entries = 0
            presentmon_stop_disallowed_log_entries = $unexpectedStopOutput.Count
            mean_display_fps_at_least_60 = ($meanDisplayFps -ge 60.0)
            p95_at_most_16_7_ms = ($p95Milliseconds -le 16.7)
            one_percent_low_at_least_45_fps = ($onePercentLowFps -ge 45.0)
            no_displayed_time_over_33_3_ms = ($over33Count -eq 0)
            no_two_consecutive_displayed_times_over_16_7_ms = ($maxConsecutiveOver16 -le 1)
            display_thresholds_passed = $displayThresholdsPassed
            bounded_display_thresholds_passed = $boundedThresholdsPassed
            real_time_display_stutter_qualified = $false
            human_review_performed = $false
            gate1_route_qualified = $false
            gate1_qualified = $false
        }
        artifacts = [ordered]@{
            ready = Get-ArtifactDescriptor -Path $readyPath -RelativePath $readyFileName
            start_signal = Get-ArtifactDescriptor -Path $signalPath -RelativePath $signalFileName
            host_boundary = Get-ArtifactDescriptor -Path $hostBoundaryPath -RelativePath $hostBoundaryFileName
            runtime_ledger = Get-ArtifactDescriptor -Path $ledgerPath -RelativePath $ledgerFileName
            presentmon_csv = Get-ArtifactDescriptor -Path $csvPath -RelativePath "presentmon.csv"
            runtime_stdout = Get-ArtifactDescriptor -Path $runtimeStdoutPath -RelativePath "runtime.stdout.log"
            runtime_stderr = Get-ArtifactDescriptor -Path $runtimeStderrPath -RelativePath "runtime.stderr.log"
            presentmon_raw_stdout = Get-ArtifactDescriptor -Path $presentMonRawStdoutPath -RelativePath "presentmon.raw.stdout.log"
            presentmon_stdout = Get-ArtifactDescriptor -Path $presentMonStdoutPath -RelativePath "presentmon.stdout.log"
            presentmon_stderr = Get-ArtifactDescriptor -Path $presentMonStderrPath -RelativePath "presentmon.stderr.log"
            presentmon_stop = Get-ArtifactDescriptor -Path $presentMonStopPath -RelativePath "presentmon.stop.log"
        }
        limitations = [ordered]@{
            single_run_only = $true
            fifo_diagnostic_non_qualifying = ($presentationContract.Name -eq "FifoDiagnostic")
            packaged_release_asset_layout_qualified = $false
            target_gpu_qualified = $false
            human_review_performed = $false
            gate1_route_qualified = $false
            gate1_qualified = $false
        }
    }
    if ($presentationContract.MarkerRequired) {
        $validation.artifacts["fifo_diagnostic_marker"] = Get-ArtifactDescriptor -Path $fifoDiagnosticMarkerPath -RelativePath $fifoDiagnosticMarkerFileName
    }
    Write-JsonAtomically -Path $validationPath -Value $validation
    if ($presentationContract.Name -eq "AutoNoVsync") {
        Assert-Condition -Condition $boundedThresholdsPassed -Message "PresentMon bounded display sample or continuous native-window-state thresholds failed; see $validationPath."
        Write-Output ("FORGE_GATE1_GAMEPLAY_STUTTER_BOUNDED_SAMPLE_RECORDED route=auto_no_vsync thresholds_passed=true samples={0} mean_ms={1:F4} mean_display_fps={2:F3} p95_ms={3:F4} one_percent_low_fps={4:F3} over_16_7={5} over_33_3={6} max_consecutive_over_16_7={7}" -f $displayedTimes.Count, $meanMilliseconds, $meanDisplayFps, $p95Milliseconds, $onePercentLowFps, $over16Count, $over33Count, $maxConsecutiveOver16)
    }
    else {
        Write-Output ("FORGE_GATE1_FIFO_DIAGNOSTIC_COMPLETE status={0} qualifying=false samples={1} mean_ms={2:F4} mean_display_fps={3:F3} p95_ms={4:F4} one_percent_low_fps={5:F3} not_foreground_samples={6}" -f $validationStatus, $displayedTimes.Count, $meanMilliseconds, $meanDisplayFps, $p95Milliseconds, $onePercentLowFps, $nativeWindowContinuity.NotForegroundSamples)
    }
}
finally {
    $presentMonSessionCleanupSucceeded = $true
    if (
        $presentMonSessionOwned -and
        -not $presentMonSessionStopped -and
        -not [string]::IsNullOrWhiteSpace([string]$sessionId)
    ) {
        try {
            $presentMonFallbackStopResult = Invoke-CapturedProcessBounded `
                -FilePath $presentMonFullPath `
                -ArgumentList @("--session_name", $sessionId, "--terminate_existing_session") `
                -WorkingDirectory $outputPath `
                -Label "PresentMon fallback session-stop control" `
                -LogPath $presentMonFallbackStopPath `
                -ExitTimeoutMilliseconds ($presentMonExitTimeoutSeconds * 1000) `
                -DrainTimeoutMilliseconds $drainTimeoutMilliseconds
            $presentMonSessionCleanupSucceeded = -not $presentMonFallbackStopResult.TimedOut -and
                $presentMonFallbackStopResult.TerminationConfirmed -and
                $presentMonFallbackStopResult.StdoutDrained -and
                $presentMonFallbackStopResult.StderrDrained -and
                $presentMonFallbackStopResult.ExitCode -eq 0
            $presentMonSessionStopped = $presentMonSessionCleanupSucceeded
        }
        catch {
            $presentMonSessionCleanupSucceeded = $false
        }
    }
    $presentMonStopped = Stop-CapturedProcessBounded -Capture $presentMonCapture
    $runtimeStopped = Stop-CapturedProcessBounded -Capture $runtimeCapture
    if ($null -ne $presentMonCapture -and -not $presentMonCapture.OutputSaved) {
        [void](Save-CapturedProcessOutput -Capture $presentMonCapture -DrainTimeoutMilliseconds $drainTimeoutMilliseconds)
    }
    if ($null -ne $runtimeCapture -and -not $runtimeCapture.OutputSaved) {
        [void](Save-CapturedProcessOutput -Capture $runtimeCapture -DrainTimeoutMilliseconds $drainTimeoutMilliseconds)
    }
    if ($null -ne $presentMonCapture) {
        $presentMonCapture.Process.Dispose()
    }
    if ($null -ne $runtimeCapture) {
        $runtimeCapture.Process.Dispose()
    }
    if ($presentMonSessionOwned -and -not [string]::IsNullOrWhiteSpace([string]$sessionId)) {
        try {
            Assert-OwnedGate1PresentMonEtwSessionAbsent `
                -SessionId $sessionId `
                -ActiveSessions @(Get-ActiveGate1PresentMonEtwSessions) `
                -Phase "cleanup"
        }
        catch {
            $presentMonSessionCleanupSucceeded = $false
        }
    }
    if ($etwGuardMutexHeld -and $null -ne $etwGuardMutex) {
        try { $etwGuardMutex.ReleaseMutex() } catch {}
    }
    if ($null -ne $etwGuardMutex) {
        $etwGuardMutex.Dispose()
    }
    if (-not $presentMonSessionCleanupSucceeded -or -not $presentMonStopped -or -not $runtimeStopped) {
        Write-Warning "A process-tree watchdog could not confirm bounded termination."
    }
}
