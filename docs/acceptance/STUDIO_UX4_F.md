# Acceptation Studio — UX4-F Animation et règles de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-011` à
`ORF-CRE-015`. La page **Créatures** expose une fiche spécialisée pour le graphe d'animation,
l'état initial, les ressources, les attributs, les défenses et les sauvegardes.

## Preuves communes

- portail incrémental réussi en **106,73 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-f-creature-animation-rules.summary.json` ;
- build Studio sans avertissement et **32/32 tests** : Domain 17, Application 9, RpgKit Domain 4
  et ModuleFormat 2 ;
- smoke Studio global réussi : sélection explicite, invalidités localisées, dry-run, empreintes,
  stale draft/approval, commande canonique, application UI réelle, undo/redo, save/reopen,
  préservation, restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD du fallback inspectée dans
  `ux4-f-creature-animation-rules-fhd.png`, puis cinq captures de la vraie Crypte inspectées dans
  `ux4-f-animation-real-crypt-fhd.png`, `ux4-f-initial-resources-real-crypt-fhd.png`,
  `ux4-f-attributes-real-crypt-fhd.png`, `ux4-f-defenses-real-crypt-fhd.png` et
  `ux4-f-preview-real-crypt-fhd.png` ;
- les cinq onglets et le footer transactionnel complet restent visibles à 1920×1080 sans UUID,
  JSON ni clipping bloquant. La provenance distingue explicitement la vraie Crypte du fallback
  canonique temporaire.

### ORF-CRE-011 — Animation set

La surface édite tous les états et transitions du `CreatureAnimationGraphProfile`, leurs catégories,
clips, conditions et profil de retargeting. Le validateur exige les familles locomotion, combat,
emote et additive et résout chaque extrémité de transition ; l'ordre et les identités stables sont
conservés pendant le round-trip.

### ORF-CRE-012 — État initial et pose

État de vie, boucle d'animation, pose et invisibilité initiale sont édités ensemble. Les
combinaisons invalides sont bloquées avant application, sans déclencher de pose ou d'animation dans
Godot.

### ORF-CRE-013 — Attributs

Le ruleset, les bases, modificateurs sourcés et formules dérivées Sum/Product/Ratio sont éditables
par collections complètes. L'aperçu appelle le validateur et l'évaluateur canoniques ; aucune
formule arbitraire ou code de ruleset n'est exécuté.

### ORF-CRE-014 — Santé et ressources

Chaque pool conserve son identité, maximum, courant, régénération et persistance. Les valeurs non
finies, maximums non positifs, courants hors bornes et régénérations négatives sont refusés. Santé,
endurance et ressources additionnelles passent par l'unique `SetCreatureFoundationHandler`.

### ORF-CRE-015 — Défenses et sauvegardes

Évitement, armure, mitigation fixe, résistances, sauvegardes et immunités sont édités sans clamp
silencieux. La prévisualisation calcule une mitigation déterministe via le contrat Domain et
conserve les capacités et l'inventaire voisins, qui ne sont pas encore exposés par ce lot.

## Limites honnêtes

- l'aperçu résout les profils Domain et RPG Kit en mémoire ; aucun clip, pose, modèle, attaque,
  acteur, moteur de combat ou transport réseau n'est exécuté ;
- les capacités et l'inventaire relèvent de `ORF-CRE-021` et `ORF-CRE-022` : ils sont préservés mais
  non édités ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- cette preuve ne qualifie pas les routes de progression, capacités et inventaire, documentées
  séparément par UX4-G et UX4-H ; `ORF-CRE-023` à `ORF-CRE-049` restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-f-creature-animation-rules -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreatureFoundation|FullyQualifiedName~CreatureAuthoringComposition|FullyQualifiedName~CreatureRulesComposition|FullyQualifiedName~SetCreatureFoundation' `
  -Capture artifacts/verification/studio-increment/ux4-f-creature-animation-rules-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-f-preview -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-f-preview-real-crypt-fhd.png `
  -Wait -SkipBuild
```
