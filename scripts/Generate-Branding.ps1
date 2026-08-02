[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeOutput = Join-Path $repositoryRoot 'apps\Forge.Runtime\branding\openrpg_forge_splash.png'
$toolsetOutput = Join-Path $repositoryRoot 'apps\Forge.Toolset\branding\openrpg_forge_splash.png'
$width = 1280
$height = 720

foreach ($outputPath in @($runtimeOutput, $toolsetOutput)) {
    $outputDirectory = Split-Path -Parent $outputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$bitmap = New-Object System.Drawing.Bitmap(
    $width,
    $height,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $backgroundRectangle = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $backgroundRectangle,
        [System.Drawing.ColorTranslator]::FromHtml('#0B0912'),
        [System.Drawing.ColorTranslator]::FromHtml('#211421'),
        90.0)
    $graphics.FillRectangle($background, $backgroundRectangle)

    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse(480, 70, 320, 320)
    $glow = New-Object System.Drawing.Drawing2D.PathGradientBrush($glowPath)
    $glow.CenterColor = [System.Drawing.Color]::FromArgb(115, 255, 112, 56)
    $glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 112, 56))
    $graphics.FillPath($glow, $glowPath)

    $centerX = 640.0
    $centerY = 220.0
    $radius = 92.0
    $points = New-Object 'System.Drawing.PointF[]' 6
    for ($index = 0; $index -lt 6; $index++) {
        $angle = ((60 * $index) - 90) * [Math]::PI / 180
        $points[$index] = New-Object System.Drawing.PointF(
            [float]($centerX + ($radius * [Math]::Cos($angle))),
            [float]($centerY + ($radius * [Math]::Sin($angle))))
    }

    $markBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF(548, 128, 184, 184)),
        [System.Drawing.ColorTranslator]::FromHtml('#FFB347'),
        [System.Drawing.ColorTranslator]::FromHtml('#E34B32'),
        45.0)
    $graphics.FillPolygon($markBrush, $points)

    $innerBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.ColorTranslator]::FromHtml('#17101B'))
    $graphics.FillPolygon($innerBrush, @(
        (New-Object System.Drawing.PointF(640, 157)),
        (New-Object System.Drawing.PointF(696, 220)),
        (New-Object System.Drawing.PointF(640, 283)),
        (New-Object System.Drawing.PointF(584, 220))))

    $runePen = New-Object System.Drawing.Pen(
        [System.Drawing.ColorTranslator]::FromHtml('#FFE1A3'),
        12)
    $runePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $runePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($runePen, 610, 245, 610, 188)
    $graphics.DrawLine($runePen, 610, 188, 672, 188)
    $graphics.DrawLine($runePen, 610, 216, 657, 216)

    $centered = New-Object System.Drawing.StringFormat
    $centered.Alignment = [System.Drawing.StringAlignment]::Center
    $centered.LineAlignment = [System.Drawing.StringAlignment]::Center
    $titleFont = New-Object System.Drawing.Font(
        'Segoe UI Semibold',
        54,
        [System.Drawing.FontStyle]::Bold,
        [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = New-Object System.Drawing.Font(
        'Segoe UI',
        18,
        [System.Drawing.FontStyle]::Regular,
        [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.ColorTranslator]::FromHtml('#FFF8ED'))
    $subtitleBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.ColorTranslator]::FromHtml('#CBBCCF'))

    $graphics.DrawString(
        'OpenRPG_Forge',
        $titleFont,
        $titleBrush,
        (New-Object System.Drawing.RectangleF(140, 342, 1000, 86)),
        $centered)
    $graphics.DrawString(
        'FORGEZ VOS MONDES  |  RACONTEZ VOS HISTOIRES',
        $subtitleFont,
        $subtitleBrush,
        (New-Object System.Drawing.RectangleF(140, 430, 1000, 44)),
        $centered)

    $linePen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(120, 255, 149, 76),
        2)
    $graphics.DrawLine($linePen, 490, 510, 790, 510)

    $temporaryOutput = "$runtimeOutput.tmp"
    $bitmap.Save($temporaryOutput, [System.Drawing.Imaging.ImageFormat]::Png)
    Move-Item -LiteralPath $temporaryOutput -Destination $runtimeOutput -Force
    Copy-Item -LiteralPath $runtimeOutput -Destination $toolsetOutput -Force
}
finally {
    $linePen.Dispose()
    $subtitleBrush.Dispose()
    $titleBrush.Dispose()
    $subtitleFont.Dispose()
    $titleFont.Dispose()
    $centered.Dispose()
    $runePen.Dispose()
    $innerBrush.Dispose()
    $markBrush.Dispose()
    $glow.Dispose()
    $glowPath.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "OPENRPG_FORGE_BRANDING_GENERATED=$runtimeOutput"
