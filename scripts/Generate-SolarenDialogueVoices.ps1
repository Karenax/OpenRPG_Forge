[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EspeakRoot,
    [string]$WorkspaceRoot,
    [string]$ArtifactDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\solaren'
}
if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot 'artifacts\audio\solaren\espeak-ng-1.52.0'
}

$workspace = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$artifacts = [System.IO.Path]::GetFullPath($ArtifactDirectory)
$toolRoot = [System.IO.Path]::GetFullPath($EspeakRoot)
$executable = Join-Path $toolRoot 'espeak-ng.exe'
$data = Join-Path $toolRoot 'espeak-ng-data'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or
    -not (Test-Path -LiteralPath $data -PathType Container)) {
    throw "EspeakRoot must contain espeak-ng.exe and espeak-ng-data: $toolRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $workspace 'project.json') -PathType Leaf)) {
    throw "Solaren workspace is invalid: $workspace"
}

$takes = @(
    [pscustomobject]@{ Name = 'maelis-01-repere'; Text = 'La pluie a éteint le repère. Peux-tu le rallumer ?' },
    [pscustomobject]@{ Name = 'maelis-02-voyageurs'; Text = 'Il guide les voyageurs lorsque la brume couvre Aubeciel.' },
    [pscustomobject]@{ Name = 'maelis-03-sud-place'; Text = 'Le repère se dresse au sud de la place. Rallume-le, puis reviens me voir.' },
    [pscustomobject]@{ Name = 'maelis-04-reviens'; Text = 'Reviens quand la lumière sera revenue.' }
)

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$results = foreach ($take in $takes) {
    $artifact = Join-Path $artifacts ($take.Name + '.wav')
    & $executable "--path=$toolRoot" -v 'fr+f3' -s 145 -p 55 -a 120 -g 3 -w $artifact $take.Text
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $artifact -PathType Leaf)) {
        throw "eSpeak NG failed to generate $($take.Name).wav (exit $LASTEXITCODE)."
    }

    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    $relative = "audio/dialogues/blobs/sha256/$($hash.Substring(0, 2))/$hash.wav"
    $destination = Join-Path $workspace ($relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $existingHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($existingHash -ne $hash) {
            throw "Content-addressed destination contains different bytes: $destination"
        }
    }
    else {
        Copy-Item -LiteralPath $artifact -Destination $destination
    }

    [pscustomobject][ordered]@{
        name = $take.Name
        text = $take.Text
        resource = $relative
        mimeType = 'audio/wav'
        bytes = (Get-Item -LiteralPath $artifact).Length
        contentSha256 = $hash
        generator = 'eSpeak NG 1.52.0'
        voice = 'fr+f3'
        speed = 145
        pitch = 55
        amplitude = 120
        wordGap = 3
    }
}

$results | ConvertTo-Json -Depth 4
