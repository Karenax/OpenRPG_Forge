# Acceptation Studio — UX4-B Factions vivantes

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-FAC-006` à
`ORF-FAC-010`. Depuis **Contenu social**, la fiche **Factions vivantes** expose réputation
personnelle et globale, propagation par témoins, affiliations multiples et factions dynamiques.
La sélection d'une faction est explicite ; l'aperçu canonique reste obligatoire avant application.

## Preuves communes

- portail `scripts/Verify-StudioIncrement.ps1` réussi en **102,95 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-b-faction-runtime.summary.json` ;
- build Studio Debug sans avertissement et **51/51 tests factions** : Domain 14, Application 13,
  RpgKit Domain 20 et ModuleFormat 4 ;
- smoke Studio global réussi, y compris toutes les surfaces UX0–UX2, UX4-A et UX5-A : dry-run,
  double garde révision/empreintes, application, undo/redo, sauvegarde/réouverture, préservation
  des voisins, restauration de session/approbation/surface et suppression du workspace temporaire ;
- capture Forward+ FHD du fallback isolé :
  `artifacts/verification/studio-increment/ux4-b-faction-runtime-fhd.png` ;
- capture Forward+ FHD des données réellement authorées de la Crypte des Cendres, dans une copie
  temporaire : `artifacts/verification/studio-increment/ux4-b-faction-runtime-real-crypt-fhd.png` ;
- revue humaine des deux captures : quatre onglets, contexte, champs directs, aperçu, diagnostics et
  cinq actions transactionnelles visibles sans clipping ; origine de la preuve explicite et aucun
  UUID affiché.

### ORF-FAC-006 — Réputation personnelle

Les overlays par sujet conservent type, sujet, source, cible, variation, expiration et cause. Les
valeurs sont éditées directement, sans clamp silencieux ; `null` et collection configurée restent
distincts dans le catalogue et au round-trip.

### ORF-FAC-007 — Réputation globale

Les transactions persistent scope, clé de scope, source, cible, variation, cause et date. L'aperçu
utilise `FactionReputationService` pour exposer le score calculé, tandis que
`SetFactionCatalogHandler` demeure l'unique autorité de mutation et d'historique.

### ORF-FAC-008 — Propagation par témoins

Chaque politique conserve faction, seuils de perception et de preuve, rayon, mémoire, variation et
propagation aux alliés. Les diagnostics canoniques bloquent les bornes incohérentes avant commande.

### ORF-FAC-009 — Affiliations multiples

Un même acteur peut conserver plusieurs affiliations ordonnées avec rang, identité de couverture,
loyauté, poids, statut principal et expiration. Le smoke prouve au moins deux memberships sans
écraser les parents, relations, bandes ni profils voisins.

### ORF-FAC-010 — Factions dynamiques

La surface édite faction, type, période d'activité, révision et états diplomatiques directionnels.
Les diplomaties sont sélectionnées explicitement et restent attachées à leur faction dynamique ;
leur décision runtime est prévisualisée sans muter d'acteur ni de réseau.

## Limites honnêtes

- le Studio authorise et prévisualise des politiques ; il ne propage pas un événement de gameplay,
  ne modifie aucun acteur et ne simule aucun transport réseau ;
- le fallback n'ajoute rien à Forgotten Forest. La preuve riche vit uniquement dans un workspace
  temporaire supprimé après le test ;
- la capture Crypte utilise les données réelles dans une copie isolée : la source, son historique,
  son état dirty, sa sélection et ses approbations restent inchangés ;
- `ORF-FAC-011` à `ORF-FAC-014` restent `planned` jusqu'à leur propre surface et leurs preuves.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)

& .\scripts\Verify-StudioIncrement.ps1 -Page social-content `
  -Evidence ux4-b-faction-runtime -TestProject $projects `
  -TestFilter 'FullyQualifiedName~Faction' `
  -Capture artifacts/verification/studio-increment/ux4-b-faction-runtime-fhd.png

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page social-content -Evidence ux4-b-faction-runtime -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-b-faction-runtime-real-crypt-fhd.png `
  -Wait -SkipBuild
```
