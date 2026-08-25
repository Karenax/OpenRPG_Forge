[CmdletBinding()]
param(
    [string]$OutputPath
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\solaren'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot 'artifacts\solaren\solaren-les-vallees-libres.orpmod'
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath (Join-Path $workspace 'project.json') -PathType Leaf)) {
    throw "Le workspace Solaren est introuvable : $workspace"
}

[System.IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null

& dotnet run --project (Join-Path $repositoryRoot 'tools\Forge.Cli') -- validate $workspace
if ($LASTEXITCODE -ne 0) {
    throw "La validation canonique de Solaren a échoué avec le code $LASTEXITCODE."
}

& dotnet run --project (Join-Path $repositoryRoot 'tools\Forge.Cli') -- build $workspace $OutputPath
if ($LASTEXITCODE -ne 0) {
    throw "La construction du package Solaren a échoué avec le code $LASTEXITCODE."
}

Write-Output (
    "OPENRPG_FORGE_SOLAREN_CONTENT_PASS workspace=$workspace " +
    "source=creator-workspace validation=canonical package=$OutputPath")
