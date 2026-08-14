[CmdletBinding()]
param(
    [string]$OutputRoot
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeRoot = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$workspaceRoot = Join-Path $runtimeRoot 'content\forgotten-forest'
$enginePath = Get-ForgeEnginePath
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\verification\connected-world-visuals'
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

$captures = @(
    @{ Slug = 'forgotten-forest'; Spawn = '594a888d-da8c-5054-b546-a376cfae3ef1' },
    @{ Slug = 'clairval'; Spawn = '12b0f544-5c1d-0e58-b479-42d924e8602c' },
    @{ Slug = 'ember-inn'; Spawn = '5074454f-dc83-d656-889a-00dbef3ebf84' },
    @{ Slug = 'gnoll-fortress'; Spawn = '78d74a13-bdf4-0256-bf84-00e94a60132f' },
    @{ Slug = 'amber-desert'; Spawn = '5cb46fba-20d8-615e-99ca-16c4c01cd2c5' },
    @{ Slug = 'snow-pass'; Spawn = '9d7067a8-b830-535e-b073-d6d2d6dd1d35' }
)

foreach ($capture in $captures) {
    $pngPath = Join-Path $OutputRoot ($capture.Slug + '.png')
    $logPath = Join-Path $OutputRoot ($capture.Slug + '.log')
    $process = Start-Process `
        -FilePath $enginePath `
        -ArgumentList @(
            '--rendering-method', 'forward_plus',
            '--path', $runtimeRoot,
            '--log-file', $logPath,
            '--',
            '--workspace', $workspaceRoot,
            '--spawn-id', $capture.Spawn,
            '--world-visual-capture', $pngPath) `
        -WorkingDirectory $runtimeRoot `
        -WindowStyle Hidden `
        -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $passed = $false
    do {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $logPath) {
            $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
            $passed = $log -match "OPENRPG_FORGE_AV6E_WORLD_VISUAL_PASS.*area=$($capture.Slug)"
            if ($log -match 'OPENRPG_FORGE_RUNTIME_STARTUP_FAILED') { break }
        }
    } while (!$passed -and [DateTime]::UtcNow -lt $deadline)

    if (!$passed -or !(Test-Path -LiteralPath $pngPath)) {
        throw "Visual capture failed for '$($capture.Slug)'. See $logPath."
    }
    $bytes = [System.IO.File]::ReadAllBytes($pngPath).Length
    if ($bytes -lt 50000) { throw "Visual capture for '$($capture.Slug)' is unexpectedly small." }
    Write-Output "CONNECTED_WORLD_VISUAL_PASS area=$($capture.Slug) bytes=$bytes file=$pngPath"
}
