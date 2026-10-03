param(
    [Parameter(Mandatory = $true)][string]$Reference,
    [Parameter(Mandatory = $true)][string]$Native,
    [Parameter(Mandatory = $true)][string]$Output,
    [ValidateSet('Village', 'Seaward')][string]$Preset = 'Village',
    [ValidateRange(1, 64)][int]$Step = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$referencePath = (Resolve-Path -LiteralPath $Reference).Path
$nativePath = (Resolve-Path -LiteralPath $Native).Path
$outputPath = [System.IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) {
    throw "Refusing to overwrite comparison: $outputPath"
}
if (-not (Test-Path -LiteralPath (Split-Path -Parent $outputPath) -PathType Container)) {
    throw "Output directory does not exist: $outputPath"
}

function Assert-PngSignature([string]$Path) {
    $expected = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    $actual = [byte[]]::new(8)
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        if ($stream.Read($actual, 0, 8) -ne 8) { throw "PNG header too short: $Path" }
    } finally { $stream.Dispose() }
    for ($i = 0; $i -lt 8; $i++) {
        if ($actual[$i] -ne $expected[$i]) { throw "Not a PNG file: $Path" }
    }
}
Assert-PngSignature $referencePath
Assert-PngSignature $nativePath

$sourceImage = [System.Drawing.Bitmap]::new($referencePath)
$nativeImage = [System.Drawing.Bitmap]::new($nativePath)
try {
    if ($sourceImage.Width -ne 1920 -or $sourceImage.Height -ne 1080 -or
        $nativeImage.Width -ne 1920 -or $nativeImage.Height -ne 1080) {
        throw 'Paired captures must both be 1920x1080 pixels.'
    }

    $regions = if ($Preset -eq 'Village') {
        @(
            @{ name = 'hill'; x0 = 400; x1 = 1500; y0 = 100; y1 = 330 },
            @{ name = 'houses'; x0 = 200; x1 = 1700; y0 = 380; y1 = 620 },
            @{ name = 'foreground'; x0 = 450; x1 = 1550; y0 = 780; y1 = 1030 },
            @{ name = 'whole_image'; x0 = 0; x1 = 1920; y0 = 0; y1 = 1080 }
        )
    } else {
        @(
            @{ name = 'sky'; x0 = 400; x1 = 1600; y0 = 0; y1 = 330 },
            @{ name = 'open_water'; x0 = 1000; x1 = 1650; y0 = 380; y1 = 550 },
            @{ name = 'beach'; x0 = 500; x1 = 1500; y0 = 740; y1 = 1050 },
            @{ name = 'whole_image'; x0 = 0; x1 = 1920; y0 = 0; y1 = 1080 }
        )
    }

    $measurements = foreach ($region in $regions) {
        $sourceLuma = 0.0
        $nativeLuma = 0.0
        $absoluteLuma = 0.0
        $absoluteRgb = 0.0
        $changed = 0
        $samples = 0
        for ($y = $region.y0; $y -lt $region.y1; $y += $Step) {
            for ($x = $region.x0; $x -lt $region.x1; $x += $Step) {
                $s = $sourceImage.GetPixel($x, $y)
                $n = $nativeImage.GetPixel($x, $y)
                $ls = 0.2126 * $s.R + 0.7152 * $s.G + 0.0722 * $s.B
                $ln = 0.2126 * $n.R + 0.7152 * $n.G + 0.0722 * $n.B
                $dr = [Math]::Abs([int]$s.R - [int]$n.R)
                $dg = [Math]::Abs([int]$s.G - [int]$n.G)
                $db = [Math]::Abs([int]$s.B - [int]$n.B)
                $sourceLuma += $ls
                $nativeLuma += $ln
                $absoluteLuma += [Math]::Abs($ls - $ln)
                $absoluteRgb += ($dr + $dg + $db) / 3.0
                if ([Math]::Max($dr, [Math]::Max($dg, $db)) -gt 3) { $changed++ }
                $samples++
            }
        }
        [ordered]@{
            name = $region.name
            rectangle = @($region.x0, $region.y0, $region.x1, $region.y1)
            samples = $samples
            reference_luma_mean = [Math]::Round($sourceLuma / $samples, 4)
            native_luma_mean = [Math]::Round($nativeLuma / $samples, 4)
            luma_absolute_error_mean = [Math]::Round($absoluteLuma / $samples, 4)
            rgb_absolute_error_mean = [Math]::Round($absoluteRgb / $samples, 4)
            fraction_changed_over_3 = [Math]::Round($changed / $samples, 6)
        }
    }

    $report = [ordered]@{
        schema = 'forge.tidewater.paired-capture-comparison'
        version = 1
        reference = $referencePath
        reference_sha256 = (Get-FileHash -LiteralPath $referencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        native = $nativePath
        native_sha256 = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash.ToLowerInvariant()
        width = 1920
        height = 1080
        preset = $Preset
        sample_step_pixels = $Step
        luma_formula = '0.2126 R + 0.7152 G + 0.0722 B, sRGB 8-bit'
        regions = @($measurements)
        limitations = @(
            'Requires independently verified identical camera and source revision',
            'Temporal state, animated vegetation and water can still differ',
            'Sampled image differences do not prove visual parity or performance'
        )
    }
    $json = $report | ConvertTo-Json -Depth 8
    $stream = [System.IO.FileStream]::new($outputPath, [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
        try { $writer.WriteLine($json) } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
    Write-Output $json
} finally {
    $nativeImage.Dispose()
    $sourceImage.Dispose()
}
