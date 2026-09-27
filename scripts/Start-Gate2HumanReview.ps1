#Requires -Version 7.2
# One human-review copy; never reset or overwrite the author's work.
[CmdletBinding()]
param([switch]$PrepareOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repository 'artifacts/forge-runtime/gate2-resume-20260919/authoring'
$sourceZone = Join-Path $source '03992311-9297-5219-8aa3-ed561640a312/authoring.json'
$review = 'E:\OpenRPG_Forge\.gate2-evidence\gate2-ux-20260923\human-review'
$copy = Join-Path $review 'authoring-root'
$profile = Join-Path $review 'webview2-user-data'
$candidate = 'E:\OpenRPG_Forge\.gate2-evidence\gate2-ux-20260923\polish-final-candidate\forge-studio-candidate.exe'
$expectedSource = '89E0DCB892A81B9589A604459DE1AA1F97690CD502737F81ECAE0782E84118D7'
$expectedCandidate = 'D01F0005D8B9346E6235C9D9ACF423F24E847A4219B05DDD1AE5D7363B76E080'

if (-not (Test-Path -LiteralPath $candidate -PathType Leaf) -or
    (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $expectedCandidate) {
    throw 'Le candidat Studio attendu est absent ou a changé ; aucun lancement.'
}
if (-not (Test-Path -LiteralPath $sourceZone -PathType Leaf) -or
    (Get-FileHash -LiteralPath $sourceZone -Algorithm SHA256).Hash -ne $expectedSource) {
    throw 'La fixture source a changé ; aucune copie ni lancement.'
}
if (-not (Test-Path -LiteralPath $copy)) {
    $null = New-Item -ItemType Directory -Path $review -Force
    Copy-Item -LiteralPath $source -Destination $copy -Recurse
    $copiedZone = Join-Path $copy '03992311-9297-5219-8aa3-ed561640a312/authoring.json'
    if ((Get-FileHash -LiteralPath $copiedZone -Algorithm SHA256).Hash -ne $expectedSource) {
        throw 'La copie de travail ne correspond pas à la fixture source ; aucun lancement.'
    }
    Write-Output "Copie de test créée : $copy"
} else {
    if (-not (Test-Path -LiteralPath $copy -PathType Container)) {
        throw "Le chemin de copie de test existe mais n'est pas un dossier ; aucun lancement."
    }
    $copiedZone = Join-Path $copy '03992311-9297-5219-8aa3-ed561640a312/authoring.json'
    if (-not (Test-Path -LiteralPath $copiedZone -PathType Leaf)) {
        throw 'La copie de test est incomplète ; aucun lancement ni remplacement automatique.'
    }
    Write-Output "Copie de test existante conservée : $copy"
}

$null = New-Item -ItemType Directory -Path $profile -Force
if ($PrepareOnly) {
    Write-Output 'Copie et candidat vérifiés ; aucun Studio lancé.'
    return
}
$previous = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_USER_DATA_FOLDER')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:FORGE_AUTHORING_ROOT = $copy
    $env:FORGE_AUTHOR_SUBJECT = 'gate2-human-review'
    $env:WEBVIEW2_USER_DATA_FOLDER = $profile
    Write-Output 'Studio va ouvrir une fenêtre. Dans Projet, actualiser les zones puis ouvrir « R2-004 Final B ». Fermer Studio pour terminer.'
    $started = Get-Date
    $process = Start-Process -FilePath $candidate -PassThru
    $process.WaitForExit()
    Write-Output ("Studio fermé. Durée totale (attentes comprises) : {0:n1} min. Noter séparément le temps actif et les attentes ressenties." -f ((Get-Date) - $started).TotalMinutes)
} finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}
