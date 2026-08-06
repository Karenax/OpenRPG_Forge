# AV2 — Normalisation, miniature et qualité 3D

## Capacité livrée

- opérations Blender typées `generate_thumbnail` et `audit_quality`, ajoutées à la normalisation,
  aux collisions, LOD et export GLB existants ;
- pivot `ground_center` réellement centré en X/Y et posé à Z=0, plus pivot `object_center` ;
- publication protégée par fichiers temporaires : un échec ne remplace pas le dernier GLB ou aperçu
  valide et les originaux du Vault restent immuables ;
- démarrage Blender avec `--disable-autoexec` afin qu’un fichier artistique ne puisse pas injecter
  un script au chargement ;
- miniature PNG RGBA 512 × 512, cadrage studio sombre, éclairage proportionnel aux dimensions et
  suppression des métadonnées non déterministes ;
- audit JSON v1 : meshes, sommets, triangles, matériaux, textures manquantes, normales, dimensions,
  pivot, collisions, LOD, miniature et budgets ;
- scores licence, confiance source, technique, visuel, performance, métadonnées et global.

## Preuves

- huit tests Python du worker couvrent allowlist, confinement, hash source, GLB/PNG, rapport positif,
  refus qualité, publication atomique et canonicalisation de miniature ;
- les suites du pipeline comptent 22 tests après ajout de la tranche ;
- Blender 4.5.10 transforme réellement `ambientcg:3DApple001` : GLB 2 texturé, 500 triangles,
  252 sommets, deux LOD, une collision, deux textures résolues, aperçu 512 × 512 et score 99/100 ;
- deux rendus consécutifs ont le même SHA-256 PNG après canonicalisation ;
- `scripts/Verify.ps1` exige désormais GLB, PNG, rapport passé, hash cohérent et métriques non vides.

## Limite conservée

La capacité est prouvée sur une ressource réelle et prête pour l’industrialisation par packs. Elle
ne signifie pas que les 605 entrées du catalogue ont déjà été normalisées ni validées artistiquement.
