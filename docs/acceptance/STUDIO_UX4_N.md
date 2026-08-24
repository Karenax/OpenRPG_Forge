# Acceptation Studio — UX4-N Événements avancés de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-039`,
`ORF-CRE-041`, `ORF-CRE-042`, `ORF-CRE-044` et `ORF-CRE-045`. Ces cinq routes partagent le
document canonique `CreatureEventProfile` et la commande `SetCreatureEventsHandler`.

## Preuves communes

- portail incrémental réussi en **113,33 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-n-creature-events.summary.json` ;
- build Studio sans avertissement et **12/12 tests** : Domain 5, Application 4 et ModuleFormat 3 ;
- smoke Studio global réussi : sélection explicite, frontière de combat, inventaire, repos,
  heartbeat, payload utilisateur typé, invalidité et doublon refusés, projection runtime, dry-run,
  stale draft/approval, commande canonique, transaction UI, undo/redo, save/reopen, préservation du
  tail dégâts/mort, restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD fallback inspectée dans
  `artifacts/verification/studio-increment/ux4-n-creature-events-fhd.png` ;
- capture Forward+ FHD du **profil réel de la Crypte**, onglet événement utilisateur, inspectée dans
  `artifacts/verification/studio-increment/ux4-n-creature-events-real-crypt-custom-fhd.png` ;
- les trois onglets, le résumé canonique et les cinq actions transactionnelles restent visibles à
  1920×1080 sans UUID, JSON ni clipping.

## ORF-CRE-039 — OnCombatRoundEnd

L’auteur choisit explicitement une frontière **fin de tick de combat** ou **fin de tour**, ainsi que
son handler ForgeScript. Le Core reste indépendant d’un modèle de rounds ; l’aperçu produit seulement
un dispatch typé et ordonné avec autorité serveur.

## ORF-CRE-041 — OnDisturbed

La route G13-F couvre les événements d’inventaire perturbé : consultation et transfert d’objet
possèdent chacun un binding stable, avec acteur, cause, résultat, objet et quantité dans le payload.
Les deux handlers sont édités ensemble afin de conserver cette frontière transactionnelle.

## ORF-CRE-042 — OnRested

Le binding de fin de repos transporte durée, ressources restaurées et interruptions. La surface
n’exécute aucun repos : elle montre uniquement le handler serveur et un payload d’aperçu déterministe.

## ORF-CRE-044 — OnHeartbeat

Cadence, budget, activation, suspension hors zone d’intérêt et handler sont éditables sans clamp
silencieux. Le validateur refuse les valeurs hors bornes ; le resolver limite le rattrapage à quatre
dispatches et rend le backlog tronqué explicite.

## ORF-CRE-045 — OnUserDefined

Chaque ligne expose namespace, nom, version, caractère requis, champs typés et handler. Les UUID de
schéma/binding restent internes et stables ; noms qualifiés, versions, champs et doublons sont validés
avant le dry-run. Les types pris en charge sont String, Integer, Number, Boolean, EntityId et ItemId.

## Limites honnêtes

- aucun ForgeScript, acteur, inventaire, repos, scheduler moteur ou réseau n’est exécuté dans Studio ;
  les services Domain/Application produisent seulement des dispatches et snapshots en mémoire ;
- Crypte des Cendres ne possède aucun profil événement riche : sa capture utilise un fallback canonique
  temporaire. La capture Crypte utilise le vrai `Gardien des Cendres`. Les deux travaillent sur une
  copie isolée et la source reste inchangée ;
- le profil global reste requis ou optionnel, mais UX4-N ne retire jamais silencieusement les bindings
  dégâts/mort. `DamageDeath`, les overrides, tous les profils voisins et tous les autres blueprints
  sont préservés ;
- `ORF-CRE-036`, `ORF-CRE-043` et `ORF-CRE-049` restent au lot dégâts/mort/validation ;
  `ORF-CRE-035`, `ORF-CRE-037`, `ORF-CRE-038`, `ORF-CRE-040` et `ORF-CRE-046` restent au profil
  IA/événements ; `ORF-CRE-033` et `ORF-CRE-034` restent distinctes.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~Forge.Domain.Tests.Creatures.CreatureEventProfileTests|' +
  'FullyQualifiedName~Forge.Application.Tests.Blueprints.SetCreatureEventsHandlerTests|' +
  'FullyQualifiedName~Forge.Application.Tests.Creatures.CreatureEventScriptServiceTests|' +
  'FullyQualifiedName~Deserialize_MigratesPreCreatureEventsFormatWithEmptyEventProfile|' +
  'FullyQualifiedName~SerializeAndDeserialize_RoundTripsCreatureEventBindings|' +
  'FullyQualifiedName~ExportValidateAndLoad_PreservesCanonicalCreatureEvents'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-n-creature-events -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-n-creature-events-fhd.png `
  -NoMatrixCheck
```
