param(
    [string[]]$Baseline,
    [string[]]$Changed,
    [string[]]$Restored,
    [int[]]$Region,
    [int[]]$ControlRegion,
    [int]$Step = 2
)

# Read-only RGB diagnostic. Separation is not photometric or artistic qualification.
$ErrorActionPreference = 'Stop'

function Get-SampleDistance($Left, $Right) {
    if ($Left.Count -eq 0 -or $Left.Count -ne $Right.Count) { throw 'Incompatible samples' }
    $total = 0.0
    for ($i = 0; $i -lt $Left.Count; $i++) {
        $total += [Math]::Abs([double]$Left[$i] - [double]$Right[$i])
    }
    return $total / $Left.Count
}

function Get-OpticalSeparation($A, $B, $R) {
    if ($A.Count -lt 3 -or $B.Count -lt 3 -or $R.Count -lt 3) {
        throw 'At least three independent captures per state are required'
    }
    $noise = 0.0
    foreach ($group in @($A, $B, $R)) {
        for ($i = 0; $i -lt $group.Count; $i++) {
            for ($j = $i + 1; $j -lt $group.Count; $j++) {
                $noise = [Math]::Max($noise, (Get-SampleDistance $group[$i] $group[$j]))
            }
        }
    }
    $effect = [double]::PositiveInfinity
    foreach ($left in $A) {
        foreach ($right in $B) {
            $effect = [Math]::Min($effect, (Get-SampleDistance $left $right))
        }
    }
    $restoration = 0.0
    foreach ($left in $A) {
        foreach ($right in $R) {
            $restoration = [Math]::Max($restoration, (Get-SampleDistance $left $right))
        }
    }
    # One 8-bit code value is a conservative quantization floor, not measurement precision.
    $floor = [Math]::Max(1.0, $noise)
    return [ordered]@{
        within_state_max_rgb_channel_mae = $noise
        changed_min_rgb_channel_mae = $effect
        restored_max_rgb_channel_mae = $restoration
        separated_and_restored = ($effect -gt 3 * $floor -and $restoration -le 2 * $floor)
    }
}

function Read-OpticalRegion($Bitmap, [int[]]$Box, [int]$Stride) {
    if ($Box.Count -ne 4 -or $Stride -lt 1 -or $Box[0] -lt 0 -or $Box[1] -lt 0 -or
        $Box[2] -le $Box[0] -or $Box[3] -le $Box[1] -or
        $Box[2] -gt $Bitmap.Width -or $Box[3] -gt $Bitmap.Height) { throw 'Invalid ROI or step' }
    $values = [System.Collections.Generic.List[byte]]::new()
    for ($y = $Box[1]; $y -lt $Box[3]; $y += $Stride) {
        for ($x = $Box[0]; $x -lt $Box[2]; $x += $Stride) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $values.Add($pixel.R); $values.Add($pixel.G); $values.Add($pixel.B)
        }
    }
    return ,$values.ToArray()
}

if ($MyInvocation.InvocationName -ne '.') {
    Add-Type -AssemblyName System.Drawing
    $hashes = [System.Collections.Generic.HashSet[string]]::new()
    $records = @(); $regions = @(); $controls = @(); $dimensions = $null
    foreach ($paths in @($Baseline, $Changed, $Restored)) {
        if ($paths.Count -lt 3) { throw 'At least three independent captures per state are required' }
        $samples = @(); $witnesses = @()
        foreach ($path in $paths) {
            $resolved = (Resolve-Path -LiteralPath $path).Path
            $hash = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            if (-not $hashes.Add($hash)) { throw 'Duplicate image content cannot count as an independent sample' }
            $bitmap = [System.Drawing.Bitmap]::new($resolved)
            try {
                $size = "$($bitmap.Width)x$($bitmap.Height)"
                if ($dimensions -and $size -ne $dimensions) { throw 'Capture dimensions differ' }
                $dimensions = $size
                $samples += ,(Read-OpticalRegion $bitmap $Region $Step)
                $witnesses += ,(Read-OpticalRegion $bitmap $ControlRegion $Step)
                $records += @{ path = $resolved; sha256 = $hash }
            } finally { $bitmap.Dispose() }
        }
        $regions += ,$samples; $controls += ,$witnesses
    }
    $signal = Get-OpticalSeparation $regions[0] $regions[1] $regions[2]
    $control = Get-OpticalSeparation $controls[0] $controls[1] $controls[2]
    [ordered]@{
        schema = 'forge.optical-sample-comparison'; schema_version = 1
        qualification = 'not_qualified'; capture_state_identity_verified = $false
        note = 'Heuristic only; inspect images, source/camera/settings identity and control contamination separately'
        dimensions = $dimensions; region = $Region; control_region = $ControlRegion; step = $Step
        signal = $signal; control = $control; captures_in_baseline_changed_restored_order = $records
    } | ConvertTo-Json -Depth 8
}
