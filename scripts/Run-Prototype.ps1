[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$enginePath = Get-ForgeEnginePath

Start-Process `
    -FilePath $enginePath `
    -ArgumentList @('--path', $runtimePath) `
    -WorkingDirectory $runtimePath

Write-Output 'Prototype launched: Crypte des Cendres.'
