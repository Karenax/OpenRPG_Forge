[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$statusPath = Join-Path $repositoryRoot 'STATUS.md'
$contextPath = Join-Path $repositoryRoot 'PROJECT_CONTEXT.md'
$trackerPath = Join-Path $repositoryRoot 'tracking/project/AURORA_PARITY_COVERAGE.csv'
$graphPath = Join-Path $repositoryRoot 'docs/architecture/graph.json'

$status = Get-Content -LiteralPath $statusPath -Raw
$context = Get-Content -LiteralPath $contextPath -Raw
$tracker = @(Import-Csv -LiteralPath $trackerPath -Delimiter ';')
$graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

$tested = @($tracker | Where-Object status -eq 'Tested')
if ($tracker.Count -ne 717 -or $tested.Count -ne $tracker.Count) {
    $failures.Add("Aurora tracker is not fully closed: tested=$($tested.Count) total=$($tracker.Count).")
}

$reputation = @($tracker | Where-Object feature_id -eq 'ORF-FAC-006')
if ($reputation.Count -ne 1 -or $reputation[0].status -ne 'Tested') {
    $failures.Add('ORF-FAC-006 must have one canonical Tested row in the Aurora tracker.')
}

if ($status -notmatch '717/717 fonctions au statut `Tested`') {
    $failures.Add('STATUS.md does not report the canonical 717/717 Tested summary.')
}
if ($status -match 'ORF-FAC-006` reste explicitement `planned`') {
    $failures.Add('STATUS.md still contains the obsolete ORF-FAC-006 planned assertion.')
}
if ($status -match 'graphe Roslyn courant[^\r\n]*\d[\d ]+\s+n[œo]uds') {
    $failures.Add('STATUS.md hard-codes volatile current architecture graph counters.')
}
if ($context -match 'La reconstruction spatiale actuelle reste en qualification') {
    $failures.Add('PROJECT_CONTEXT.md still presents the integrated Crypt reconstruction as pre-merge work.')
}
if (@($graph.diagnostics).Count -ne 0) {
    $failures.Add("The generated architecture graph contains $(@($graph.diagnostics).Count) diagnostics.")
}

if ($failures.Count -gt 0) {
    throw "Project status consistency failed:`n- $($failures -join "`n- ")"
}

Write-Output "OPENRPG_FORGE_PROJECT_STATUS_CONSISTENCY_PASS tracker=$($tracker.Count) tested=$($tested.Count) graph_diagnostics=0"
