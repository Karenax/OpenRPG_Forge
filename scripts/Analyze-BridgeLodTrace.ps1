param([Parameter(Mandatory)][string]$EvidenceDirectory, [switch]$RequireScreenProjection)
$ErrorActionPreference = 'Stop'
$report = Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'manifest.json') -Raw | ConvertFrom-Json
$logPath = Join-Path $EvidenceDirectory 'runtime.jsonl'
if ((Get-FileHash -LiteralPath $logPath).Hash -ine $report.runtime_log_sha256) { throw 'Log hash mismatch' }
$events = @(Get-Content -LiteralPath $logPath | Where-Object { $_.StartsWith('{') } | ConvertFrom-Json)
if ($events | Where-Object event_kind -eq 'bridge_lod_trace_unavailable') { throw 'Incomplete trace' }
$rootsEvent = @($events | Where-Object event_kind -eq 'bridge_lod_trace_roots')
$frames = @($events | Where-Object event_kind -eq 'bridge_lod_camera_sample')
if ($rootsEvent.Count -ne 1 -or $frames.Count -ne $report.recording.frames) { throw 'Trace coverage mismatch' }
for ($i = 0; $i -lt $frames.Count; $i++) {
    if ($frames[$i].frame -ne $i) { throw 'Frame sequence mismatch' }
    foreach ($axis in @('x','y','z')) {
        if ($null -eq $frames[$i].$axis -or -not [double]::IsFinite($frames[$i].$axis)) { throw 'Invalid camera' }
    }
    if ($RequireScreenProjection) {
        if ([string]::IsNullOrWhiteSpace($frames[$i].clip_from_world_json)) { throw 'Camera projection missing' }
        $matrix = @($frames[$i].clip_from_world_json | ConvertFrom-Json)
        if ($matrix.Count -ne 16) { throw 'Camera projection missing' }
        foreach ($value in $matrix) { if ($null -eq $value -or -not [double]::IsFinite($value)) { throw 'Invalid camera projection' } }
        $frames[$i] | Add-Member -NotePropertyName matrix -NotePropertyValue $matrix
    }
}
$roots = @($rootsEvent[0].roots_json | ConvertFrom-Json)
if ($roots.Count -eq 0 -or @($roots.id | Sort-Object -Unique).Count -ne $roots.Count) { throw 'Invalid tree identities' }
$intervals = foreach ($tree in $roots) {
    if ($tree.root.Count -ne 3) { throw 'Invalid root' }
    foreach ($coordinate in $tree.root) { if (-not [double]::IsFinite($coordinate)) { throw 'Invalid root coordinate' } }
    $minimum = [double]::PositiveInfinity
    $maximum = 0.0
    foreach ($frame in $frames) {
        $dx = $frame.x - $tree.root[0]
        $dy = $frame.y - $tree.root[1]
        $dz = $frame.z - $tree.root[2]
        $distance = [Math]::Sqrt($dx*$dx + $dy*$dy + $dz*$dz)
        $minimum = [Math]::Min($minimum, $distance)
        $maximum = [Math]::Max($maximum, $distance)
    }
    [pscustomobject]@{ id=$tree.id; root=$tree.root; minimum_m=$minimum; maximum_m=$maximum }
}
$bands = foreach ($name in @('near','far')) {
    $start = $rootsEvent[0]."${name}_start"
    $end = $rootsEvent[0]."${name}_end"
    if ($null -eq $start -or $null -eq $end -or $start -ge $end) { throw 'Invalid fade interval' }
    $crossed = @($intervals | Where-Object { $_.minimum_m -le $start -and $_.maximum_m -ge $end })
    if ($RequireScreenProjection) {
        foreach ($tree in $crossed) {
            $samples = @(foreach ($frame in $frames) {
                $dx=$frame.x-$tree.root[0]; $dy=$frame.y-$tree.root[1]; $dz=$frame.z-$tree.root[2]
                $distance=[Math]::Sqrt($dx*$dx+$dy*$dy+$dz*$dz)
                if ($distance -lt $start -or $distance -gt $end) { continue }
                $m=$frame.matrix; $p=$tree.root
                $clip=@(foreach($row in 0..3){$m[$row]*$p[0]+$m[$row+4]*$p[1]+$m[$row+8]*$p[2]+$m[$row+12]})
                if($clip[3] -le 0){continue}
                $x=$clip[0]/$clip[3]; $y=$clip[1]/$clip[3]; $z=$clip[2]/$clip[3]
                if([Math]::Abs($x) -le 1 -and [Math]::Abs($y) -le 1 -and $z -ge 0 -and $z -le 1){
                    [pscustomobject]@{frame=$frame.frame; distance_m=$distance; x_fraction=($x+1)/2; y_fraction=(1-$y)/2}
                }
            })
            $tree | Add-Member -NotePropertyName root_in_view_sample_count -NotePropertyValue $samples.Count
            $selected = @()
            if ($samples.Count -gt 0) {
                $selected = @(@(0, [int][Math]::Floor($samples.Count/2), ($samples.Count-1)) | Sort-Object -Unique | ForEach-Object { $samples[$_] })
            }
            $tree | Add-Member -NotePropertyName root_in_view_samples -NotePropertyValue $selected
        }
    }
    [pscustomobject]@{ band=$name; start_m=$start; end_m=$end; full_distance_crossings=$crossed.Count; trees=$crossed }
}
[pscustomobject]@{
    schema='forge.bridge-lod-distance-audit'; schema_version=2
    frames=$frames.Count; trees=$roots.Count; bands=$bands
    scope='logical_root_distance_only_not_frustum_visibility_or_gpu_lod_execution'
    root_projection_included=[bool]$RequireScreenProjection
    projection_scope='root_point_inside_clip_volume_only_not_crown_visibility_or_occlusion'
    human_accepted=$false; gate_passed=$false
} | ConvertTo-Json -Depth 8
