[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$runtimeProject = Join-Path $runtimePath 'Forge.Runtime.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$runtimeLog = Join-Path $artifactRoot 'runtime-interactive.log'
$enginePath = Get-ForgeEnginePath

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
& dotnet build $runtimeProject --configuration Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw "La compilation du prototype a échoué avec le code $LASTEXITCODE."
}

Start-Process `
    -FilePath $enginePath `
    -ArgumentList @('--path', $runtimePath, '--log-file', $runtimeLog) `
    -WorkingDirectory $runtimePath

Write-Output "Prototype lancé : Crypte des Cendres. Journal : $runtimeLog"
