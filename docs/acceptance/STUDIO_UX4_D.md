# Acceptation Studio — UX4-D Fondation de créature

État local au 20 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-001` à
`ORF-CRE-005` et `ORF-CRE-033`. La page **Créatures** expose une fiche spécialisée pour sélectionner une créature,
initialiser ou modifier sa fondation, ses textes localisés, son rôle, son niveau de menace et sa
composition d'espèce.

## Preuves communes

- portail incrémental réussi en **101,74 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-d-creature-foundation.summary.json` ;
- build Studio sans avertissement et **77/77 tests** : Domain 13, Application 9, RpgKit Domain 10
  et ModuleFormat 45 ;
- smoke Studio global réussi : sélection explicite, initialisation contrôlée, dry-run, empreintes,
  stale draft/approval, application, undo/redo, save/reopen, préservation et cleanup ;
- captures Forward+ FHD inspectées : fallback temporaire dans
  `artifacts/verification/studio-increment/ux4-d-creature-foundation-fhd.png`, vraie fondation de la
  Crypte dans `ux4-d-creature-foundation-real-crypt-fhd.png`, assistant guidé dans
  `ux4-d-creature-wizard-fhd.png` et aperçu éditorial dans `ux4-d-creature-preview-fhd.png` ;
- les quatre onglets, le résumé, les diagnostics et les cinq actions restent visibles sans
  clipping ; la provenance de la copie est explicite et aucun UUID n'est affiché.
- complément OnSpawn réussi en **154,82 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-d-creature-onspawn.summary.json`, avec build sans
  avertissement, **20/20 tests ciblés**, smoke global et capture FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-d-creature-onspawn-fhd.png`.

### ORF-CRE-001 — Assistant de créature

L'assistant renseigne asset, archétype, ruleset, rig, faction humaine, rôle, menace, kit visuel et
tags de combat. Les valeurs alimentent `CreatureAuthoringCompositionProfile` puis passent par le
validateur et la commande canonique ; aucune formule propriétaire de règles n'est imposée.

### ORF-CRE-002 — Blueprint de créature

Une créature ou un point de départ joueur est choisi explicitement. Une fondation absente n'est
créée qu'après aperçu approuvé et application de `SetCreatureFoundationHandler`. Le slug, l'UUID,
la présentation, la progression, le loot, les comportements, les événements et le combat voisins
restent protégés.

### ORF-CRE-003 — Identité et texte

La fiche édite nom français et anglais, alias, pronoms, biographies, notes d'auteur et tags
narratifs. Les collections localisées et les identités cachées sont conservées pendant
l'application, l'annulation, le rétablissement et le round-trip.

### ORF-CRE-004 — Rôle et défi

Le rôle de rencontre et le niveau de menace sont validés puis projetés par
`CreatureAuthoringCompositionFoundationService`. L'aperçu affiche une composition éditoriale ; il
ne prétend ni lancer un combat, ni instancier un acteur.

### ORF-CRE-005 — Race, espèce et sous-type

La surface édite l'espèce, ses parents, ensembles de traits, variantes et squelette requis sans
réécrire les profils de règles, interaction ou IA événementielle. Les références héritées non
éditables restent préservées plutôt que normalisées silencieusement.

### ORF-CRE-033 — OnSpawn

Le handler `creature.spawned` est édité directement dans la fondation sans remplacer son UUID, son
schéma, sa version ForgeScript ni son autorité serveur. L'aperçu appelle réellement
`CreatureFoundationValidator.Spawn` après validation complète, initialise les ressources et produit
un événement typé sur une copie en mémoire. Aucun acteur, nœud Godot, script ou réseau n'est lancé.

## Limites honnêtes

- l'aperçu est une projection data-driven : aucun modèle ou asset 3D n'est chargé ;
- aucun script, moteur de combat, navigation, animation ou transport réseau n'est exécuté ;
- les captures utilisent des copies temporaires. Source, historique, état dirty, sélection,
  approbations, page et surface active sont restaurés exactement ;
- OnConversation est qualifié séparément par UX4-J ; cette preuve seule ne qualifie pas les objets,
  marchands ou compagnons.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-d-creature-foundation -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreatureFoundation|FullyQualifiedName~CreatureAuthoringComposition|FullyQualifiedName~CreatureFactory|FullyQualifiedName~ProjectJsonSerializerTests' `
  -Capture artifacts/verification/studio-increment/ux4-d-creature-foundation-fhd.png

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-d-creature-foundation -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-d-creature-foundation-real-crypt-fhd.png `
  -Wait -SkipBuild
```
