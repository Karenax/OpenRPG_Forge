[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
$enginePath = Get-ForgeEnginePath

Start-Process `
    -FilePath $enginePath `
    -ArgumentList @('--editor', '--path', $toolsetPath) `
    -WorkingDirectory $toolsetPath

Write-Output 'OpenRPG Forge Toolset launched.'
