# Acceptation Studio — UX4-O Dégâts, mort et validation créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-036`,
`ORF-CRE-043` et `ORF-CRE-049`. Ces trois routes partagent le tail canonique
`CreatureEventProfile.DamageDeath`, la commande `SetCreatureEventsHandler` et la validation
projet ciblée du blueprint.

## Preuves communes

- portail incrémental réussi en **124,08 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-o-creature-damage-validation.summary.json` ;
- build Studio sans avertissement et **22/22 tests ciblés** : Domain 5, RPG Kit 11,
  Application 4 et ModuleFormat 2 ;
- smoke Studio global réussi : sélection explicite, BeforeDamage, DamageApplied, EntityDied,
  validation créature, retrait optionnel, budgets/ratios invalides refusés, dry-run, projection
  runtime, stale draft/approval, commande canonique, transaction UI, undo/redo, save/reopen,
  préservation UX4-N, restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD des champs dégâts/mort fallback inspectée dans
  `artifacts/verification/studio-increment/ux4-o-creature-damage-fhd.png` ;
- capture Forward+ FHD fallback de la validation inspectée dans
  `artifacts/verification/studio-increment/ux4-o-creature-damage-validation-fhd.png` ;
- capture Forward+ FHD du **profil réel de la Crypte**, onglet validation, inspectée dans
  `artifacts/verification/studio-increment/ux4-o-creature-damage-validation-real-crypt-fhd.png` ;
- les deux onglets, les cinq champs, le résumé canonique et les cinq actions transactionnelles
  restent visibles à 1920×1080 sans UUID, JSON ni clipping.

## ORF-CRE-036 — OnDamaged

La surface expose les handlers **BeforeDamage** et **DamageApplied**. Le premier est borné par un
budget de 1 à 50 ms et un ratio d’ajustement fini entre 0 et 1 ; le second transporte le résultat
post-transaction complet. L’aperçu Domain produit les deux dispatches sans modifier de ressource,
d’acteur ou de points de vie.

## ORF-CRE-043 — OnDeath

Le binding **EntityDied** est édité dans le même cycle transactionnel. Son aperçu expose killer,
contributeurs, cause, position, loot et politique de drop après la transaction, sans faire apparaître
les UUID internes ni exécuter de loot ou de script.

## ORF-CRE-049 — Validation de créature

Avant la commande, la surface agrège `CreatureEventValidator`, `ProjectValidator` et le dry-run de
`SetCreatureEventsHandler` pour la créature ciblée. Les diagnostics conservent leurs codes et chemins
canoniques en mémoire, tandis que la surface les traduit en messages humains. Fondation,
présentation, progression, loot, rôle, règles et références sont revalidés avec le profil événement.

## Préservation et limites honnêtes

- combat, inventaire, repos, heartbeat et événements utilisateur UX4-N sont conservés sans
  réécriture ; overrides, profils voisins et autres blueprints restent inchangés ;
- Crypte des Cendres ne possède pas ce profil riche : sa preuve utilise une copie temporaire avec
  fallback canonique. La Crypte utilise le vrai `Gardien des Cendres`. La source reste strictement
  inchangée dans les deux parcours ;
- aucun dégât, acteur, loot, ForgeScript ou transport réseau n’est exécuté dans Studio ; la surface
  qualifie l’authoring, la validation, la persistance et les dispatches purs ;
- `ORF-CRE-035`, `ORF-CRE-037`, `ORF-CRE-038`, `ORF-CRE-040` et `ORF-CRE-046` restent au lot
  IA/événements ; `ORF-CRE-033` et `ORF-CRE-034` restent des autorités distinctes.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreatureEventProfileTests|' +
  'FullyQualifiedName~CreatureCombatAndAuthoringTests|' +
  'FullyQualifiedName~CreatureFactoryTests|' +
  'FullyQualifiedName~SetCreatureEventsHandlerTests|' +
  'FullyQualifiedName~CreatureEventScriptServiceTests|' +
  'FullyQualifiedName~SerializeAndDeserialize_RoundTripsCreatureEventBindings|' +
  'FullyQualifiedName~ExportValidateAndLoad_PreservesCanonicalCreatureEvents'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-o-creature-damage-validation -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-o-creature-damage-validation-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-o-validation -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-o-creature-damage-validation-real-crypt-fhd.png `
  -Wait -SkipBuild
```
