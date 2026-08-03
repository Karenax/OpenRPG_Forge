[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Title,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ForgeWindowCaptureNative {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@

$target = [IntPtr]::Zero
$callback = [ForgeWindowCaptureNative+EnumWindowsProc]{
    param([IntPtr]$handle, [IntPtr]$state)
    if (-not [ForgeWindowCaptureNative]::IsWindowVisible($handle)) { return $true }
    $builder = [Text.StringBuilder]::new(512)
    [void][ForgeWindowCaptureNative]::GetWindowText($handle, $builder, $builder.Capacity)
    if ($builder.ToString() -eq $Title) { $script:target = $handle; return $false }
    return $true
}
[void][ForgeWindowCaptureNative]::EnumWindows($callback, [IntPtr]::Zero)
if ($target -eq [IntPtr]::Zero) { throw "Window '$Title' was not found." }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
function Save-Capture([string]$name) {
    $rect = [ForgeWindowCaptureNative+Rect]::new()
    if (-not [ForgeWindowCaptureNative]::GetWindowRect($target, [ref]$rect)) { throw 'GetWindowRect failed.' }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height)); $bitmap.Save((Join-Path $OutputDirectory $name), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

[void][ForgeWindowCaptureNative]::SetForegroundWindow($target)
Start-Sleep -Milliseconds 350
Save-Capture 'g8-author-ui-nominal.png'
[void][ForgeWindowCaptureNative]::ShowWindow($target, 3)
Start-Sleep -Milliseconds 500
Save-Capture 'g8-author-ui-maximized.png'
[void][ForgeWindowCaptureNative]::ShowWindow($target, 9)
Write-Output "OPENRPG_FORGE_VISUAL_CAPTURE_PASS title=$Title"
