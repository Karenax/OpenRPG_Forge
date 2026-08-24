[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$budgets = [ordered]@{
    'apps/Forge.Toolset/addons/openrpg_forge/ForgeEditorPlugin.cs' = 21725
    'apps/Forge.Runtime/Runtime/GameRoot.cs' = 8200
    'apps/Forge.Runtime/Runtime/GameRoot.Smoke.cs' = 6600
    'src/Forge.Mcp/Projects/ForgeProjectAgentService.cs' = 8500
}
$failures = [System.Collections.Generic.List[string]]::new()
$measurements = [System.Collections.Generic.List[string]]::new()

foreach ($entry in $budgets.GetEnumerator()) {
    $path = Join-Path $repositoryRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Maintainability budget target is missing: $($entry.Key).")
        continue
    }
    $lineCount = @(Get-Content -LiteralPath $path).Count
    $measurements.Add("$($entry.Key)=$lineCount/$($entry.Value)")
    if ($lineCount -gt $entry.Value) {
        $failures.Add("$($entry.Key) has $lineCount lines; budget is $($entry.Value). Extract a focused component instead of growing the monolith.")
    }
}

if ($failures.Count -gt 0) {
    throw "Maintainability budget failed:`n- $($failures -join "`n- ")"
}

Write-Output "OPENRPG_FORGE_MAINTAINABILITY_BUDGET_PASS $($measurements -join ' ')"
