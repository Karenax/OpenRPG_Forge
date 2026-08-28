[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$budgets = [ordered]@{
    'apps/Forge.Toolset/addons/openrpg_forge/ForgeEditorPlugin.cs' = 600
    'apps/Forge.Runtime/Runtime/GameRoot.cs' = 1900
    'apps/Forge.Runtime/Runtime/GameRoot.Smoke.cs' = 500
    'src/Forge.Mcp/Projects/ForgeProjectAgentService.cs' = 8500
}
$familyBudgets = @(
    [pscustomobject]@{
        Directory = 'apps/Forge.Toolset/addons/openrpg_forge'
        Pattern = 'ForgeEditorPlugin*.cs'
        Budget = 2200
    },
    [pscustomobject]@{
        Directory = 'apps/Forge.Runtime/Runtime'
        Pattern = 'GameRoot*.cs'
        Budget = 2000
    },
    [pscustomobject]@{
        Directory = 'apps/Forge.Studio/Session'
        Pattern = 'StudioSessionController*.cs'
        Budget = 2000
    }
)
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

foreach ($family in $familyBudgets) {
    $directory = Join-Path $repositoryRoot $family.Directory
    $files = @(Get-ChildItem -LiteralPath $directory -Filter $family.Pattern -File)
    if ($files.Count -eq 0) {
        $failures.Add("Maintainability budget family is empty: $($family.Directory)/$($family.Pattern).")
        continue
    }
    $counts = @($files | ForEach-Object {
        [pscustomobject]@{
            Path = [System.IO.Path]::GetRelativePath($repositoryRoot, $_.FullName).Replace('\\', '/')
            Lines = @(Get-Content -LiteralPath $_.FullName).Count
        }
    })
    $largest = $counts | Sort-Object Lines -Descending | Select-Object -First 1
    $measurements.Add("$($family.Pattern)=max:$($largest.Lines)/$($family.Budget),files:$($counts.Count)")
    foreach ($item in $counts | Where-Object Lines -GT $family.Budget) {
        $failures.Add("$($item.Path) has $($item.Lines) lines; family budget is $($family.Budget). Extract a focused partial or service.")
    }
}

if ($failures.Count -gt 0) {
    throw "Maintainability budget failed:`n- $($failures -join "`n- ")"
}

Write-Output "OPENRPG_FORGE_MAINTAINABILITY_BUDGET_PASS $($measurements -join ' ')"
