[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Workspace,
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$TemplateDirectory = 'artifacts/templates/game-windows-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($name in @('Workspace', 'OutputDirectory', 'TemplateDirectory')) {
    $value = Get-Variable -Name $name -ValueOnly
    if (-not [System.IO.Path]::IsPathRooted($value)) {
        Set-Variable -Name $name -Value (Join-Path $repositoryRoot $value)
    }
}
$workspacePath = [System.IO.Path]::GetFullPath($Workspace)
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$templatePath = [System.IO.Path]::GetFullPath($TemplateDirectory)
if (-not (Test-Path -LiteralPath $workspacePath -PathType Container)) {
    throw "The game workspace does not exist: $workspacePath"
}
if (-not (Test-Path -LiteralPath $templatePath -PathType Container)) {
    throw "The game launcher template does not exist: $templatePath"
}
if (Test-Path -LiteralPath $outputPath) {
    throw "The game distribution destination already exists: $outputPath"
}

$cliProject = Join-Path $repositoryRoot 'tools/Forge.Cli/Forge.Cli.csproj'
& dotnet run --project $cliProject --no-restore -- launcher build $workspacePath $templatePath $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "Game distribution build failed with exit code $LASTEXITCODE."
}
& dotnet run --project $cliProject --no-restore -- launcher verify $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "Game distribution verification failed with exit code $LASTEXITCODE."
}

Write-Output "OPENRPG_FORGE_GAME_DISTRIBUTION_READY path=$outputPath verified=true"
