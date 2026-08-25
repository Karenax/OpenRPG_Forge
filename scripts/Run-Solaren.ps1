[CmdletBinding()]
param(
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$runtimeProject = Join-Path $runtimePath 'Forge.Runtime.csproj'
$workspace = Join-Path $runtimePath 'content\solaren'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\solaren'
$runtimeLog = Join-Path $artifactRoot 'runtime-interactive.log'
$enginePath = Get-ForgeEnginePath

if (-not (Test-Path -LiteralPath (Join-Path $workspace 'project.json') -PathType Leaf)) {
    throw "Le workspace Solaren n'existe pas encore : $workspace"
}
[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
if (-not $SkipBuild) {
    & dotnet build $runtimeProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "La compilation de Solaren a échoué avec le code $LASTEXITCODE."
    }
}

$process = Start-Process `
    -FilePath $enginePath `
    -ArgumentList @(
        '--rendering-method', 'forward_plus',
        '--path', $runtimePath,
        '--log-file', $runtimeLog,
        '--', '--workspace', $workspace
    ) `
    -WorkingDirectory $runtimePath `
    -PassThru

Write-Output "Solaren lancé : Les Vallées Libres. PID : $($process.Id). Exécutable : $enginePath. Journal : $runtimeLog"
