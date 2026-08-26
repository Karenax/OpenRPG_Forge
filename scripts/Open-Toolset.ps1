[CmdletBinding()]
param(
    [string[]]$UserArgument = @(),
    [switch]$Headless,
    [switch]$Wait,
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsetPath = Join-Path $repositoryRoot 'apps\Forge.Toolset'
$toolsetProject = Join-Path $toolsetPath 'Forge.Toolset.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$toolsetLog = Join-Path $artifactRoot 'toolset-interactive.log'
$enginePath = Get-ForgeEnginePath

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
if (-not $SkipBuild) {
    & dotnet build $toolsetProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "La compilation du Toolset a échoué avec le code $LASTEXITCODE."
    }
}

$arguments = @('--rendering-method', 'forward_plus')
if ($Headless) { $arguments += '--headless' } else { $arguments += '--fullscreen' }
$arguments += @('--editor', '--path', $toolsetPath, '--log-file', $toolsetLog)
if ($UserArgument.Count -gt 0) { $arguments += '--'; $arguments += $UserArgument }
$launch = @{
    FilePath = $enginePath
    ArgumentList = $arguments
    WorkingDirectory = $toolsetPath
    PassThru = $true
}
if ($Headless) { $launch.WindowStyle = 'Hidden' }
$process = Start-Process @launch
if ($Wait) { $process.WaitForExit() }

Write-Output "OpenRPG Forge Toolset lancé avec Forward+. Journal : $toolsetLog"
