# Acceptation Studio — UX4-C Matrice et validation factionnelles

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-FAC-011` à
`ORF-FAC-014`. La page **Contenu social** expose une fiche spécialisée pour tags sémantiques,
matrice directionnelle et historique, réaction au joueur et validation factionnelle.

## Preuves communes

- portail incrémental réussi en **108,39 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-c-faction-matrix.summary.json` ;
- build Studio sans avertissement et **51/51 tests factions** : Domain 14, Application 13,
  RpgKit Domain 20 et ModuleFormat 4 ;
- smoke Studio global réussi : sélection explicite, import/export canonique, dry-run, empreintes,
  stale draft/approval, application, undo/redo, save/reopen, préservation et cleanup ;
- captures Forward+ FHD inspectées : fallback temporaire dans
  `artifacts/verification/studio-increment/ux4-c-faction-matrix-fhd.png` et données réelles de la
  Crypte dans `artifacts/verification/studio-increment/ux4-c-faction-matrix-real-crypt-fhd.png` ;
- les quatre onglets, l’aperçu, le diagnostic et les cinq actions restent visibles sans clipping,
  avec origine de preuve explicite et aucun UUID affiché.

### ORF-FAC-011 — Tags de faction

Les tags directs sont édités sur une faction explicitement sélectionnée. Les tags effectifs hérités
sont résolus par `FactionRuntimeResolver`; identifiants non normalisés et doublons sont bloqués par
`FactionValidator` avant la commande.

### ORF-FAC-012 — Interface de matrice

Chaque direction source→cible conserve score, explication et preset. La fiche capture un historique
versionné, compare deux snapshots, simule les dispositions et importe/exporte le JSON canonique sans
créer de stockage parallèle.

### ORF-FAC-013 — Réaction au joueur

La politique configure scope et chaîne réputation→déguisement→crimes→dialogue→zone→MJ. L’aperçu
utilise `FactionReputationService.ResolveReaction` sur un état copié ; aucun acteur, dialogue,
règle de zone ou override MJ réel n’est muté.

### ORF-FAC-014 — Validation factionnelle

La surface restitue les diagnostics canoniques localisés : cycles d’héritage, parents absents,
bandes chevauchantes, membres orphelins, tags, relations, snapshots et réaction. Le smoke exerce
explicitement membre orphelin, chevauchement de bandes et tag invalide.

## Limites honnêtes

- la heatmap et la réaction sont des calculs déterministes en mémoire, pas une rencontre moteur ;
- import/export concerne un snapshot de matrice, jamais le projet entier ;
- les captures utilisent des copies temporaires. Source, historique, état dirty, sélection,
  approbations et surface active sont restaurés exactement ;
- la fermeture des quatorze routes FACTION ne qualifie aucune route CREATURE, ITEM, MERCHANT ou
  COMPANION restante de UX4.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page social-content `
  -Evidence ux4-c-faction-matrix -TestProject $projects `
  -TestFilter 'FullyQualifiedName~Faction' `
  -Capture artifacts/verification/studio-increment/ux4-c-faction-matrix-fhd.png

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page social-content -Evidence ux4-c-faction-matrix -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-c-faction-matrix-real-crypt-fhd.png `
  -Wait -SkipBuild
```
