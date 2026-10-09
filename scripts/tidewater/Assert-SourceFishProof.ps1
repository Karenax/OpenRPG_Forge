# Shared native-walk and packaging contract. This checks MainWorld preparation,
# not GPU completion, visible pixels or source visual parity.
function Assert-TidewaterSourceFishProof {
    param([Parameter(Mandatory)][object]$WalkProof)
    foreach ($field in @('pass', 'source_fish_requested', 'source_fish_preparation_pass', 'source_fish')) {
        if ($WalkProof.PSObject.Properties.Name -notcontains $field) {
            throw "Preuve native des poissons source incomplète : $field"
        }
    }
    if ($null -eq $WalkProof.source_fish) { throw 'Reçu natif des poissons source absent.' }
    $fish = $WalkProof.source_fish
    foreach ($field in @('staged', 'source_ids', 'village_source_ids', 'visual_lod_slots', 'masked_shadow_meshes', 'prepared_frames', 'game_package_sha256', 'surface_package_sha256')) {
        if ($fish.PSObject.Properties.Name -notcontains $field) {
            throw "Reçu natif des poissons source incomplet : $field"
        }
    }
    if ($WalkProof.pass -ne $true -or $WalkProof.source_fish_requested -ne $true -or
        $WalkProof.source_fish_preparation_pass -ne $true -or $fish.staged -ne $true -or
        @($fish.source_ids).Count -ne 5 -or @($fish.village_source_ids).Count -ne 78 -or
        $fish.visual_lod_slots -ne 166 -or $fish.masked_shadow_meshes -ne 83 -or
        $fish.prepared_frames -le 0 -or
        $fish.game_package_sha256 -ne 'f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d' -or
        $fish.surface_package_sha256 -ne 'e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a') {
        throw 'Le runtime exact doit passer la promenade avec préparation des poissons source avant packaging.'
    }
    $sourcePrefix = 'tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c'
    for ($ordinal = 0; $ordinal -lt 78; $ordinal++) {
        if ($fish.village_source_ids[$ordinal] -cne "${sourcePrefix}:fish-prop:${ordinal}") {
            throw 'Identités des poissons du village différentes de la référence.'
        }
    }
    for ($ordinal = 18; $ordinal -lt 23; $ordinal++) {
        if ($fish.source_ids[$ordinal - 18] -cne "${sourcePrefix}:game-fish:${ordinal}") {
            throw 'Identités des poissons exposés différentes de la référence.'
        }
    }
}
