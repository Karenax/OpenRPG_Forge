# Acceptation Studio — UX4-M Rôle, activité et auto-level

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-030`,
`ORF-CRE-047` et `ORF-CRE-048`. La page **Créatures** expose une surface unique parce que ces
trois routes partagent le même `CreatureRoleBehaviorProfile` et la même transaction.

## Preuves communes

- portail incrémental réussi en **124,60 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-m-creature-role-behavior.summary.json` ;
- build Studio sans avertissement et **10/10 tests** : Domain 4, Application 4 et ModuleFormat 2 ;
- smoke Studio global réussi : sélection explicite, rôle/lore, graphe ambiant, auto-level, retrait
  optionnel, invalidité et doublon refusés, projection runtime, dry-run, stale draft/approval,
  commande canonique, transaction UI, undo/redo, save/reopen, préservation, restauration de
  surface et libération du workspace temporaire ;
- capture Forward+ FHD fallback inspectée dans
  `ux4-m-creature-role-behavior-fhd.png` ;
- trois captures Forward+ FHD sur le **profil réel de la Crypte** inspectent respectivement le
  rôle/lore, les animations ambiantes et l’auto-level :
  `ux4-m-creature-role-behavior-real-crypt-fhd.png`,
  `ux4-m-creature-role-behavior-real-crypt-ambient-fhd.png` et
  `ux4-m-creature-role-behavior-real-crypt-auto-level-fhd.png` ;
- les onglets restent bornés et défilables à 1920×1080, tandis que le résumé canonique et les cinq
  actions transactionnelles restent visibles sans UUID, JSON ni clipping.

## ORF-CRE-030 — Divinité et informations de rôle

La surface édite le template de ruleset, les champs de lore `clé=valeur` et les tags de rôle.
Le lore demeure explicitement descriptif : aucune règle mécanique n’est inférée d’une divinité,
d’un métier ou d’un tag. Les identifiants non canoniques et les clés dupliquées bloquent l’aperçu.

## ORF-CRE-047 — Animations ambiantes

Le graphe ambiant conserve les identités internes stables et expose tous les idle sets pondérés,
animations, durées, horaires, destinations optionnelles, conversations, interactions, cooldowns et
politiques d’interruption/reprise. `CreatureRoleBehaviorRuntimeResolver` produit à heure fixe une
sélection déterministe en mémoire ; Studio ne joue aucune animation.

## ORF-CRE-048 — Auto-level et templates

Le template expose seed, niveau cible, application transactionnelle, verrouillage manuel,
allocations de pistes, compétences et talents. La commande `SetCreatureRoleBehaviorHandler`
prévisualise le diff déterministe puis remplace, si demandé, uniquement la progression de la
créature ciblée dans la même révision annulable.

## Limites honnêtes

- aucun acteur, graphe IA, animation, script, waypoint, dialogue, sauvegarde automatique ou réseau
  n’est exécuté dans Studio ; les services Domain/Application calculent seulement la projection ;
- la capture Forgotten Forest utilise un fallback canonique temporaire, tandis que les trois
  captures Crypte utilisent le vrai profil `Gardien des Cendres` ; toutes travaillent sur une copie
  isolée et la source reste inchangée ;
- présentation, règles, capacités, loot, événements, fondation, items et blueprints voisins sont
  préservés ; la progression n’est remplacée que lorsque `ApplyOnSave` est explicitement actif ;
- `ORF-CRE-046` reste distinct : son IA par profil appartient à
  `CreatureEventAiCompositionProfile`, pas à ce lot ;
- cette preuve ne qualifie pas `ORF-CRE-033` à `ORF-CRE-046` ni `ORF-CRE-049`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreatureRoleBehaviorProfileTests|' +
  'FullyQualifiedName~SetCreatureRoleBehaviorHandlerTests|' +
  'FullyQualifiedName~CreatureRoleBehaviorScriptServiceTests|' +
  'FullyQualifiedName~SerializeAndDeserialize_RoundTripsCreatureRoleBehaviorAndTemplate|' +
  'FullyQualifiedName~ExportValidateAndLoad_PreservesCreatureRoleBehaviorAndAutoLevelTemplate'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-m-creature-role-behavior -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-m-creature-role-behavior-fhd.png `
  -NoMatrixCheck
```
