# Acceptation Studio — UX4-G Progression de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-016` à
`ORF-CRE-020`. La page **Créatures** expose une fiche spécialisée pour les classes et niveaux,
les axes d'alignement définis par le projet, les compétences, les talents et les pouvoirs appris
ou préparés.

## Preuves communes

- portail incrémental réussi en **102,38 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-g-creature-progression.summary.json` ;
- build Studio sans avertissement et **10/10 tests** : Domain 3, Application 4 et ModuleFormat 3 ;
- smoke Studio global réussi : sélection explicite, invalidités localisées, dry-run, empreintes,
  stale draft/approval, commande canonique, transaction UI réelle, undo/redo, save/reopen,
  préservation, restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD du fallback inspectée dans `ux4-g-creature-progression-fhd.png`, puis cinq
  captures de la vraie Crypte inspectées dans `ux4-g-classes-real-crypt-fhd.png`,
  `ux4-g-alignment-real-crypt-fhd.png`, `ux4-g-skills-real-crypt-fhd.png`,
  `ux4-g-talents-real-crypt-fhd.png` et `ux4-g-powers-real-crypt-fhd.png` ;
- les cinq onglets et le footer transactionnel complet restent visibles à 1920×1080 sans UUID,
  JSON ni clipping bloquant. La provenance distingue explicitement la vraie Crypte du fallback
  canonique temporaire.

### ORF-CRE-016 — Classes et niveaux

La surface édite toutes les pistes, leurs niveaux, plafonds, expérience et talents accordés. Les
identités restent stables et le multiclassage est data-driven. Quand un auto-level voisin est actif,
la preuve réelle conserve exactement son ensemble de pistes ; le fallback isolé exerce l'ajout d'une
seconde classe.

### ORF-CRE-017 — Alignement

Les axes, libellés négatif/positif et valeurs bornées sont définis par le projet. Aucune grille morale
propriétaire n'est imposée et les collections absentes restent un état valide jusqu'à une création
explicite.

### ORF-CRE-018 — Compétences

Rang, plafond, attribut directeur, entraînement, expertise, points et difficulté sont éditables par
collection complète. L'aperçu exécute le résolveur typé et un test de compétence déterministe, sans
agir sur un acteur.

### ORF-CRE-019 — Dons et talents

Rangs, prérequis résolus, état passif, pouvoirs accordés et hooks typés sont éditables sans code
arbitraire. Les références aux pistes, compétences, talents et pouvoirs réels sont validées avant
l'application.

### ORF-CRE-020 — Sorts et pouvoirs préparés

Les pouvoirs appris et préparés proviennent exclusivement du profil de combat de la créature.
Provider, mode, capacité, rang, charges, cooldown, ressource, coût et modificateurs sont conservés et
validés ; un simple aperçu n'ajoute jamais silencieusement un pouvoir préparé.

## Limites honnêtes

- l'aperçu résout les contrats Domain/Application en mémoire ; aucune montée de niveau, capacité,
  ressource d'acteur, animation, moteur de combat ou réplication réseau n'est exécutée ;
- les capacités avancées, inventaire, loot et comportement relèvent des routes suivantes : ils sont
  préservés mais non édités par UX4-G ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- les capacités et l'inventaire sont qualifiés séparément par UX4-H ; cette preuve ne qualifie pas
  `ORF-CRE-023` à `ORF-CRE-049`, qui restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-g-creature-progression -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreatureProgression|FullyQualifiedName~SetCreatureProgression' `
  -Capture artifacts/verification/studio-increment/ux4-g-creature-progression-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-g-powers -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-g-powers-real-crypt-fhd.png `
  -Wait -SkipBuild
```
