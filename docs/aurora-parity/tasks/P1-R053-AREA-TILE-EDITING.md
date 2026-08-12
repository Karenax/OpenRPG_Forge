# P1 R053 — Édition avancée des tuiles de zone

Lot stable : `R053` / `FC-AREA-P1-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AREA-006` — groupes de tuiles paramétriques et éditables ;
- `ORF-AREA-007` — outils d'effacement, remplacement, remappage, remplissage et sélection ;
- `ORF-AREA-009` — ajustement automatique compatible et déterministe ;
- `ORF-AREA-010` — propriétés spécialisées de tuile ;
- `ORF-AREA-011` — redimensionnement de zone avec traitement explicite des pertes.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/03_AREA.md`.

## Audit avant fermeture

| Critère source | Présent avant R053 | Limite réelle fermée par R053 |
|---|---|---|
| groupes de tuiles | groupes composés, offsets, tags et transaction atomique | paramètres, empreinte, sockets, variantes et conversion en instances éditables |
| outils de géométrie | remplacement préservant transform/tags/overrides et effacement canonique | remplacement par type, remappage de kit, remplissage, sélection par type et conservation explicite des objets attachés |
| ajustement automatique | choix stable par type, seed et UUID avec verrouillage manuel | compatibilité par socket, graine reproductible et rapport d'ambiguïtés |
| propriétés locales | overrides typés, versionnés et persistants | état, animation, matériau, lumière, collision, navigation, sockets et overrides spécialisés |
| redimensionnement | cinq ancrages, aperçu des pertes, translation, validation dry-run et undo | repositionnement explicite des pertes prédites avant application atomique |
| intégration auteur | écran Zones et commandes de fondation existaient | même commande UI/application, états visibles, aperçu, ForgeScript typé et MCP filtré plan/apply/revert |
| runtime et build | géométrie de zone chargée par le runtime | absence optionnelle sûre, absence requise diagnostiquée et refus pré-build à code stable |

## Contrat livré

- profil canonique `AreaTileEditingProfile` à UUID, schéma et références validés ;
- groupes avec paramètres, empreinte, sockets et variantes, convertibles sans perte en instances ;
- outils erase/replace/remap/fill/select réunis dans `AreaTileEditRequest` et appliqués atomiquement ;
- auto-fit SHA-256 déterministe par socket, seed et placement, avec verrou manuel et ambiguïtés visibles ;
- propriétés spécialisées état/animation/matériau/lumière/collision/navigation/socket/override ;
- redimensionnement avec ancrage, pertes prédites et plan de repositionnement explicite ;
- écran Toolset avancé, même transaction dry-run/apply, undo/redo et persistance workspace/projet ;
- snapshot ForgeScript sûr, lecture MCP filtrée et plan sémantique réversible ;
- fallback runtime optionnel et diagnostic stable si la métadonnée est requise.

## Preuves produites

- huit tests ciblés R053 : Domain 3, Application 2, ModuleFormat 2 et MCP 1 ;
- round-trip courant/legacy du projet et refus de package avant écriture ;
- capture inspectée `docs/aurora-parity/evidence/r053-area-tile-editing-ui.png` ;
- marqueurs Toolset `R053_AREA_TILE_EDITING_PASS` et runtime
  `R053_AREA_TILE_EDITING_PASS` avec l'exécutable Mono non-console ;
- tracker/backlog/docs/graphe régénérés puis `scripts/Verify.ps1` vert.

## Non-objectifs

- fonctions AREA P1 du lot R054 et suivants ;
- fonctions P2, lots Asset/Map AV7–AV10, commit ou push.
