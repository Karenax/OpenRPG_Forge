# Acceptation Studio — UX4-A Fondations de factions

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-FAC-001` à
`ORF-FAC-005`. Depuis la page **Contenu social**, la fiche **Définitions, parents, relations et
bandes** expose une édition humaine du catalogue de factions, avec sélection explicite, aperçu
canonique et historique projet. `ORF-FAC-006` ne fait pas partie de cette qualification Studio et
reste **planned** pour une future surface dédiée.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur `social-content` et la preuve
  `ux4-a-faction-foundation` en **79,97 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-a-faction-foundation.summary.json` ;
- tests ciblés : Domain 3/3, RpgKit Domain 4/4, Application 3/3 et ModuleFormat 3/3, soit **13/13** ;
- smoke Studio headless réussi : sélection explicite, création, modification, archivage,
  suppression sûre, parents et héritage, matrice directionnelle, bandes, dry-run, double garde de
  révision et d'empreinte, application, undo/redo, sauvegarde/réouverture, préservation des profils
  voisins et refus canonique des références externes ;
- preuve FHD de fallback dans
  `artifacts/verification/studio-increment/ux4-a-faction-foundation-forward-plus-fhd.png` : le
  workspace Forgotten Forest reste la source ouverte, tandis qu'une fixture canonique temporaire
  et isolée alimente uniquement la preuve ;
- preuve FHD réelle et séparée dans
  `artifacts/verification/studio-increment/ux4-a-faction-foundation-real-crypt-forward-plus-fhd.png` :
  les factions réellement authorées de la Crypte des Cendres sont chargées dans une copie
  temporaire du projet Crypt ;
- preuves réelles Crypte QHD et 4K dans
  `artifacts/verification/studio-shell/ux2-5-milestone/ux4-a-real-crypt-qhd.png` et
  `artifacts/verification/studio-shell/ux2-5-milestone/ux4-a-real-crypt-4k.png`. Les trois onglets,
  l'origine de la preuve, le résumé, la note et les quatre actions restent visibles et lisibles sans
  UUID ni chemin technique affiché ;
- les journaux `.png.log` adjacents confirment Forward+, les résolutions 1920×1080, 2560×1440 et
  3840×2160, le module réellement ouvert et `OPENRPG_FORGE_STUDIO_EVIDENCE_PREPARED valid=true` ;
- aucun portail transversal complet n'a été déclenché par l'incrément UX4-A seul : son résumé porte
  `full_gate=false` et `graph_check=false`. Le jalon intégré UX2-A/UX4-A/UX5-A a ensuite réussi
  `scripts/Verify.ps1` avec 1 988 tests .NET, 39 tests Python, le graphe Roslyn et tous les smokes.

### ORF-FAC-001 — Création et suppression de faction

La fiche crée une définition, modifie son nom humain et son slug, puis permet l'archivage ou la
suppression après aperçu. `FactionAuthoringService` planifie l'opération et
`SetFactionCatalogHandler` demeure l'autorité pour le dry-run, l'application et l'historique. Une
référence de créature, de placeable ou de filtre `TriggerAdvanced` bloque honnêtement la suppression
canonique ; aucune migration implicite n'est effectuée.

### ORF-FAC-002 — Faction parente

Les parents sont choisis par libellé humain avec sélection multiple. L'héritage des tags, des
politiques et des relations est explicite. Les cycles, parents absents et incohérences sont refusés
avant la commande, sans réécrire les autres branches du catalogue.

### ORF-FAC-003 — Matrice de réputation

La matrice présente les directions réellement authorées entre factions nommées. L'éditeur conserve
score, preset et explication, et l'aperçu restitue le nombre exact de relations affectées sans
exposer les identifiants internes.

### ORF-FAC-004 — Relations asymétriques

Chaque direction est indépendante : modifier `A → B` ne crée, ne remplace ni ne supprime `B → A`.
Le smoke couvre les deux directions et le round-trip conserve leur ordre et leurs valeurs.

### ORF-FAC-005 — Seuils hostile/neutre/allié

Les bandes exposent identifiant, minimum, maximum et priorité. Les valeurs authored restent exactes,
sans arrondi ni plafonnement silencieux ; identifiants vides, bornes inversées, chevauchements et
priorités invalides sont bloqués par la validation canonique.

## Limites honnêtes

- `ORF-FAC-006` — réputation personnelle — reste planned dans le Studio : UX4-A ne l'édite pas. La
  valeur cachée `PersonalReputation`, y compris la distinction exacte entre `null` et `[]`, est
  seulement préservée au round-trip ;
- les profils Advanced, témoins, memberships, factions dynamiques, réputation globale et autres
  extensions voisines sont préservés mais ne sont pas édités par cette fiche ;
- la surface représente le catalogue authoré et ses diagnostics ; elle ne simule ni propagation
  runtime vivante, ni réseau, ni heatmap de gameplay ;
- la suppression de preuve s'exerce uniquement sur une fixture sûre. Sur un projet produit, toute
  dépendance externe détectée demeure bloquante tant qu'elle n'est pas retirée explicitement ;
- la preuve fallback ne promeut aucune donnée synthétique dans Forgotten Forest. La session source,
  son état dirty, son historique, ses approbations et sa surface active sont restaurés exactement,
  puis le répertoire temporaire est supprimé ;
- les captures Crypte utilisent une copie temporaire isolée des données réelles : elles qualifient
  le contenu authoré sans modifier le projet source ni son historique.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~FactionFoundationProfileTests|' +
  'FullyQualifiedName~FactionFoundationTests|' +
  'FullyQualifiedName~SetFactionCatalogHandlerTests|' +
  'FullyQualifiedName~FactionPersistenceTests|' +
  'FullyQualifiedName~R096_AdvancedTriggerUsesCanonicalCommandScriptProjectionAndStableValidationDiagnostics'

# Portail incrémental, smoke et preuve FHD fallback isolée sur le workspace par défaut.
& .\scripts\Verify-StudioIncrement.ps1 -Page social-content `
  -Evidence ux4-a-faction-foundation -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-a-faction-foundation-forward-plus-fhd.png

# Captures séparées sur les données réellement authorées de la Crypte des Cendres.
$crypt = 'apps/Forge.Runtime/content/crypt-of-ashes'
& .\scripts\Open-Studio.ps1 -Workspace $crypt -Page social-content `
  -Evidence ux4-a-faction-foundation -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-a-faction-foundation-real-crypt-forward-plus-fhd.png `
  -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Workspace $crypt -Page social-content `
  -Evidence ux4-a-faction-foundation -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux4-a-real-crypt-qhd.png `
  -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Workspace $crypt -Page social-content `
  -Evidence ux4-a-faction-foundation -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux2-5-milestone/ux4-a-real-crypt-4k.png `
  -Wait -SkipBuild
```
