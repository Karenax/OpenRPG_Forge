# Acceptation Studio — UX4-E Présentation de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-006` à
`ORF-CRE-010`. La page **Créatures** expose une fiche spécialisée pour le genre et les pronoms,
les portraits gouvernés, le modèle éditorial, les parties du corps, les couleurs, les ailes et les
queues.

## Preuves communes

- portail incrémental réussi en **100,01 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-e-creature-presentation.summary.json` ;
- build Studio sans avertissement et **8/8 tests** : Domain 3, Application 2 et ModuleFormat 3 ;
- smoke Studio global réussi : sélection explicite, invalidités, dry-run, empreintes, stale
  draft/approval, commande canonique, application UI réelle, undo/redo, save/reopen, préservation,
  restauration de surface et libération du workspace temporaire ;
- cinq captures Forward+ FHD inspectées : identité dans
  `ux4-e-creature-presentation-identity-fhd.png`, portrait dans
  `ux4-e-creature-presentation-portraits-fhd.png`, modèle dans
  `ux4-e-creature-presentation-appearance-fhd.png`, couleurs/attachments dans
  `ux4-e-creature-presentation-colors-fhd.png` et aperçu dans
  `ux4-e-creature-presentation-preview-fhd.png` ;
- la capture de portail `ux4-e-creature-presentation-fhd.png` confirme à nouveau le footer
  transactionnel complet à 1920×1080 ; aucun UUID ou JSON n'est exposé.

### ORF-CRE-006 — Sexe, genre et pronoms

La fiche conserve une identité ouverte et data-driven : genre canonique, type de corps et quatre
formes grammaticales. Elle n'impose pas une liste fermée et ne réécrit pas l'identité localisée de
la fondation UX4-D.

### ORF-CRE-007 — Portrait et représentation UI

Chaque variante de portrait porte une expression, un chemin relatif sûr, des dimensions, une
licence SPDX, une empreinte SHA-256 et un texte alternatif. La preuve charge réellement dans Godot
une image gouvernée de 32×32 pixels ; la collection complète et son ordre passent le round-trip.

### ORF-CRE-008 — Apparence et modèle

La surface édite le rig requis et la politique de repli vers le modèle 3D. Le modèle reste une
référence éditoriale validée : aucun mesh, squelette, matériau ou animation n'est instancié pendant
l'aperçu Studio.

### ORF-CRE-009 — Parties du corps

Les parties du corps sont des entrées sélectionnables avec slot, asset, couche, slots masqués et
règle de clipping. Les identités internes et le rig voisin sont préservés ; aucune normalisation
silencieuse d'un profil existant n'est effectuée.

### ORF-CRE-010 — Couleurs, ailes et queues

Deux collections indépendantes couvrent les paramètres de couleur et les attachments. Ailes et
queues conservent leur slot, type, asset et rig requis. L'aperçu résume les collections sans
prétendre les monter sur un acteur runtime.

## Limites honnêtes

- l'aperçu est une projection UI du `CreaturePresentationProfile` ; aucun modèle, animation,
  moteur de rendu, acteur ou transport réseau n'est exécuté ;
- la voix et le soundset relèvent de `ORF-CRE-029` : ils sont préservés mais ne sont pas édités ;
- les données réelles auditées n'avaient pas encore de présentation de créature. Les preuves
  riches utilisent donc une copie temporaire canonique, clairement étiquetée, puis vérifient que
  source, historique, état dirty, sélection, approbation et surface sont inchangés ;
- cette preuve ne qualifie pas les règles : `ORF-CRE-011` à `ORF-CRE-015` sont qualifiés
  séparément par UX4-F ; `ORF-CRE-016` à `ORF-CRE-049` restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-e-creature-presentation -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreaturePresentation|FullyQualifiedName~SetCreaturePresentation' `
  -Capture artifacts/verification/studio-increment/ux4-e-creature-presentation-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Page creatures -Evidence ux4-e-portraits `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-e-creature-presentation-portraits-fhd.png `
  -Wait -SkipBuild
```
