# Acceptation Forge Studio — UX6-W

Date de qualification : 21 août 2026.

UX6-W rend `ORF-EE-001` et `ORF-EE-027` directement accessibles sur la page Runtime dans la
surface **Transformations runtime**. Le profil cinématique canonique reste l’unique autorité : la
surface remplace uniquement ses collections `TransformOverrides` et `Tweens`, puis passe par
`SetCinematicProfilesHandler` pour le dry-run et l’application.

<a id="orf-ee-001"></a>
## ORF-EE-001 — Transformations visuelles runtime

- sélection explicite d’une zone de contexte et d’un objet réel par libellés humains ;
- collection complète d’overrides à identités stables, sans UUID visible ;
- parent optionnel avec exclusion de la cible, position, quaternion, échelle, durée et cinq courbes
  d’interpolation ;
- autorité locale, propriétaire ou serveur, réplication et comportement de sauvegarde explicites ;
- refus des enums, durées, références, cycles et transforms non canoniques par le validateur global ;
- `TransformOverrideRuntime` réellement avancé et annulé sur une copie.

<a id="orf-ee-027"></a>
## ORF-EE-027 — Interpolation de transforms

- collection complète de tweens Transform, Paramètre matériau et État ;
- transforms initial/final, vecteurs ou couleurs RGBA et états textuels éditables directement ;
- durée, easing, autorité, sauvegarde et réplication conservés avec l’identité persistante ;
- `AnimationTweenRuntime` exerce les trois canaux, l’autorité, l’état final et l’annulation ;
- l’état vide des quatre modules réels est affiché honnêtement ; la preuve riche existe uniquement
  dans un workspace temporaire dérivé de Crypte des Cendres.

## Transaction et préservation

L’aperçu lie l’approbation à la révision du projet, à son empreinte SHA-256 et à une empreinte
binaire préfixée du profil candidat. L’application refait le dry-run avant d’enregistrer une seule
commande `set_cinematic_profiles`. Undo, redo, sauvegarde et réouverture comparent le JSON canonique
complet. Startup, séquences, cinématique de zone, autres zones et racines projet sont préservés.

La preuve UI conserve projet, historique, révision, journal, état dirty, zone, sélection et toutes
les approbations UX6 de la session source. Le workspace temporaire est supprimé avant le verdict.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **10/10** — Domain 2, Application 6 et ModuleFormat 2 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_W_PASS`, avec sélection explicite, état vide réel,
  invalidité, dry-run, trois canaux, autorité, annulation, gardes périmées, commande canonique,
  undo/redo, round-trip, voisins, texte humain, bornage, transaction UI, libération des ressources,
  source inchangée et surface restaurée ;
- portail incrémental final : **146,60 s**, build, tests, smoke global, capture Forward+ FHD et
  contrôle des 717 lignes de matrice ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-w-runtime-transforms.summary.json` ;
- captures Forward+ 1920×1080 inspectées :
  `ux6-w-runtime-transforms-forward-plus-fhd.png`,
  `ux6-w-tweens-forward-plus-fhd.png` et
  `ux6-w-runtime-preview-forward-plus-fhd.png`.

La première revue FHD a révélé un panneau trop étroit dans l’onglet Tweens. Le panneau final est
ancré avec des marges bornées sur la surface Runtime : les champs, résultats et cinq actions sont
désormais visibles sans clipping.

## Limites honnêtes

Studio exécute les runtimes purs sur des copies. Il ne modifie aucun nœud Godot, matériau, état
d’acteur, scène, serveur ou transport réseau live. Le runtime de jeu reste l’unique consommateur
visuel et autoritaire des profils persistés.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CinematicProfileTests|' +
  'FullyQualifiedName~SequencePlayerTests|' +
  'FullyQualifiedName~SetCinematicProfilesHandlerTests|' +
  'FullyQualifiedName~CinematicPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-w-runtime-transforms -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-w-runtime-transforms-forward-plus-fhd.png
```
