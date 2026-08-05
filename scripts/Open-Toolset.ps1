[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
$toolsetProject = Join-Path $toolsetPath 'Forge.Toolset.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$toolsetLog = Join-Path $artifactRoot 'toolset-interactive.log'
$enginePath = Get-ForgeEnginePath

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
& dotnet build $toolsetProject --configuration Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw "La compilation du Toolset a échoué avec le code $LASTEXITCODE."
}

Start-Process `
    -FilePath $enginePath `
    -ArgumentList @('--rendering-method', 'forward_plus', '--editor', '--path', $toolsetPath, '--log-file', $toolsetLog) `
    -WorkingDirectory $toolsetPath

Write-Output "OpenRPG Forge Toolset lancé avec Forward+. Journal : $toolsetLog"
