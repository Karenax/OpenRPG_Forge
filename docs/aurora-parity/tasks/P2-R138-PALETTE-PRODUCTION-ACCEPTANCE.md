# P2 R138 — Palettes et compatibilité artistique de production

`ORF-PAL-009` ferme la limite des favoris/récents avec des collections nommées persistantes,
ordonnées et bornées à 128 blueprints. Chaque collection porte UUID, schéma, provenance et portée
de tâche ; le profil permet upsert/suppression et le catalogue respecte l'ordre choisi.

`ORF-PAL-017` ferme la mesure manuelle résiduelle : le pipeline lit les bornes `POSITION` du GLB,
les dimensions PNG, calcule la densité texel et détecte la famille PBR avant la validation du profil
artistique. Les sources invalides produisent des diagnostics stables.

Tests ciblés : `BlueprintCatalogTests` (13/13) et `ArtisticAssetMetricExtractorTests` (2/2).
Preuve Toolset : `OPENRPG_FORGE_TOOLSET_R138_PALETTE_PRODUCTION_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
