# Opens the single local Gold candidate without compiling or copying histories.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$review = Join-Path $repository 'output/studio-gold-20260924'
$executable = Join-Path $review 'candidate/forge-studio-candidate.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'Gold candidate missing. This launcher does not build automatically.'
}
if (Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }) {
    throw 'The Gold candidate is already running; use its existing window.'
}
$env:FORGE_AUTHORING_ROOT = Join-Path $review 'authoring-root'
$env:FORGE_AUTHOR_SUBJECT = 'studio-gold-review'
$env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $review 'webview-profile'
if (-not (Test-Path -LiteralPath $env:FORGE_AUTHORING_ROOT -PathType Container)) {
    throw 'Isolated review workspace missing; refusing to use the production workspace.'
}
Start-Process -FilePath $executable -WorkingDirectory $repository -WindowStyle Hidden
