[CmdletBinding()]
param([string]$RepositoryRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$auditPaths = @(
    (Join-Path $RepositoryRoot 'tracking/project/AURORA_CORE_P0_AUDIT.csv'),
    (Join-Path $RepositoryRoot 'tracking/project/AURORA_FEATURE_IMPLEMENTATION_AUDIT.csv')
)
$missing = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($auditPath in $auditPaths) {
    if (-not (Test-Path -LiteralPath $auditPath -PathType Leaf)) {
        throw "Aurora evidence audit missing: $auditPath"
    }

    foreach ($row in Import-Csv -LiteralPath $auditPath -Delimiter ';' -Encoding UTF8) {
        foreach ($column in @('implementation_evidence', 'test_evidence')) {
            foreach ($evidence in ([string]$row.$column -split '\|')) {
                $candidate = $evidence.Trim()
                if ($candidate -notmatch '^(apps|docs|scripts|src|tests|tools)/') {
                    continue
                }

                $relativePath = ($candidate -split '#', 2)[0]
                $fullPath = Join-Path $RepositoryRoot $relativePath.Replace('/', '\\')
                $checked++
                if (-not (Test-Path -LiteralPath $fullPath)) {
                    $missing.Add("$($row.feature_id):${column}:$relativePath")
                }
            }
        }
    }
}

if ($missing.Count -gt 0) {
    throw "Aurora evidence paths are stale ($($missing.Count)):`n$($missing -join "`n")"
}

Write-Output "OPENRPG_FORGE_AURORA_EVIDENCE_PATHS_PASS checked=$checked missing=0"
