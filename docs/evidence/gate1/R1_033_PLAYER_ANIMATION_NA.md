# R1-033 — Animation joueur minimale : N/A conditionnel

Date d'audit : **2026-09-01T10:02:51Z**

Source auditée : `28985328f0d2de1fa79c705d5c3aa1bc522dbe35`

Arbre source : `ec71501d231ea6469eb97dc0af3d0230f4daf6eb`

Verdict : **N/A conditionnel pour le snapshot Gate 1 courant**.

`R1-033` demande une animation minimale **si un asset est disponible**. Ce prédicat est faux dans le
contenu gouverné courant : les neuf glTF suivis sont des éléments de décor sans animation, leur
manifeste ne déclare aucun personnage et le joueur Forge est une capsule procédurale sans asset animé.
Ce verdict ferme la route conditionnelle ; il ne prétend pas livrer un système d'animation.

## Sources contrôlées

- le [manifeste du sous-ensemble CC0](../../../assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json)
  déclare neuf modèles, uniquement de types `tree`, `shrub`, `fern`, `rock` et `detail` ;
- les neuf chemins `scene_path` du manifeste correspondent exactement aux neuf `.gltf` suivis dans
  ce répertoire ;
- chaque document glTF omet `animations`, donc le décompte normalisé est zéro ;
- la [recette joueur](../../../crates/forge_world/src/player.rs) est explicitement sans asset ;
- l'[adapter joueur Bevy/Rapier](../../../crates/forge_engine/src/player_controller.rs) construit le
  mesh avec `Capsule3d::new`, nomme l'entité `Forge player capsule` et ne référence aucune API
  `AnimationPlayer`, `AnimationGraph` ou `AnimationNode`.

## Contrôle automatisé reproductible

Commande exécutée depuis la racine du worktree détaché, sans Cargo, build, lancement graphique ni
acquisition d'asset :

```powershell
$ErrorActionPreference = 'Stop'
$manifestRelative = 'assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json'
$assetPrefix = 'assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/'
$manifest = Get-Content -Raw -LiteralPath $manifestRelative | ConvertFrom-Json -Depth 100
$repositoryGltfFiles = @(git ls-files -- '*.gltf')
$gltfFiles = @(git ls-files -- "$assetPrefix*.gltf")
$rows = foreach ($relative in $gltfFiles) {
    $document = Get-Content -Raw -LiteralPath $relative | ConvertFrom-Json -Depth 100
    [pscustomobject]@{
        File = [IO.Path]::GetFileName($relative)
        Animations = if ($null -eq $document.animations) { 0 } else { @($document.animations).Count }
    }
}
$manifestPaths = @($manifest.models | ForEach-Object { "assets/$($_.scene_path)" } | Sort-Object)
$trackedPaths = @($gltfFiles | Sort-Object)
$playerSource = Get-Content -Raw -LiteralPath 'crates/forge_engine/src/player_controller.rs'
$recipeSource = Get-Content -Raw -LiteralPath 'crates/forge_world/src/player.rs'
$allowedKinds = @('detail', 'fern', 'rock', 'shrub', 'tree')
$actualKinds = @($manifest.models.kind | Sort-Object -Unique)
$checks = [ordered]@{
    repository_tracked_gltf_count = $repositoryGltfFiles.Count
    tracked_gltf_count = $gltfFiles.Count
    repository_gltf_matches_governed_subset = (
        Compare-Object ($repositoryGltfFiles | Sort-Object) $trackedPaths
    ).Count -eq 0
    manifest_model_count = @($manifest.models).Count
    manifest_matches_tracked_gltf = (Compare-Object $manifestPaths $trackedPaths).Count -eq 0
    total_animations = ($rows | Measure-Object -Property Animations -Sum).Sum
    model_kinds = $actualKinds
    manifest_contains_only_scenery = (Compare-Object $allowedKinds $actualKinds).Count -eq 0
    character_or_player_models = @($manifest.models | Where-Object {
        $_.kind -match 'character|player' -or $_.id -match 'character|player'
    }).Count
    asset_free_recipe_marker = $recipeSource.Contains(
        'Original, asset-free recipe for the Visual Valley player and camera profile.'
    )
    procedural_capsule_mesh = $playerSource.Contains('assets.add(Capsule3d::new(')
    player_entity_name = $playerSource.Contains('Name::new("Forge player capsule")')
    animation_api_references_in_player_adapter = (
        [regex]::Matches($playerSource, 'AnimationPlayer|AnimationGraph|AnimationNode')
    ).Count
}
$rows | Format-Table -AutoSize
$checks | ConvertTo-Json -Depth 4
if (
    $checks.repository_tracked_gltf_count -ne 9 -or
    $checks.tracked_gltf_count -ne 9 -or
    -not $checks.repository_gltf_matches_governed_subset -or
    $checks.manifest_model_count -ne 9 -or
    -not $checks.manifest_matches_tracked_gltf -or
    $checks.total_animations -ne 0 -or
    -not $checks.manifest_contains_only_scenery -or
    $checks.character_or_player_models -ne 0 -or
    -not $checks.asset_free_recipe_marker -or
    -not $checks.procedural_capsule_mesh -or
    -not $checks.player_entity_name -or
    $checks.animation_api_references_in_player_adapter -ne 0
) {
    throw 'R1-033 conditional N/A proof failed'
}
```

Résultat observé :

| glTF gouverné | Animations |
|---|---:|
| `Bush_Common_Flowers.gltf` | 0 |
| `CommonTree_1.gltf` | 0 |
| `CommonTree_3.gltf` | 0 |
| `Fern_1.gltf` | 0 |
| `Flower_4_Group.gltf` | 0 |
| `Mushroom_Laetiporus.gltf` | 0 |
| `Pine_2.gltf` | 0 |
| `Rock_Medium_1.gltf` | 0 |
| `Rock_Medium_3.gltf` | 0 |

Synthèse des assertions : **9 glTF suivis dans tout le dépôt**, tous dans le sous-ensemble gouverné,
**9 modèles manifestés**, correspondance exacte,
**0 animation**, catégories limitées aux cinq rôles de décor attendus, **0 modèle personnage/joueur**,
marqueur de recette sans asset présent, capsule procédurale présente, nom d'entité attendu présent et
**0 référence aux API d'animation** dans l'adapter joueur.

## Limite et réouverture

Cette preuve n'évalue ni squelette, ni blend tree, ni playback d'animation, puisqu'aucun asset joueur
animé gouverné n'existe dans la tranche. `R1-033` doit être rouverte si un asset de personnage animé
entre dans le manifeste gouverné ou si la capsule procédurale est remplacée par un modèle animé. Il
faudra alors prouver au minimum son chargement, son animation en mouvement et la cohérence native/Studio.
