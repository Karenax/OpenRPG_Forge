# Acceptation Studio — UX4-P Événements et intelligence de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-035`,
`ORF-CRE-037`, `ORF-CRE-038`, `ORF-CRE-040` et `ORF-CRE-046`. Ces cinq routes partagent
`CreatureFoundationProfile.EventAiComposition`, la commande canonique
`SetCreatureFoundationHandler` et les projections Domain/Application/RPG Kit existantes.

## Preuves communes

- portail incrémental réussi en **117,33 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-p-creature-event-ai.summary.json` ;
- build Studio sans avertissement et **9/9 tests ciblés** : Domain 4, Application 2,
  RPG Kit 2 et ModuleFormat 1 sur le vrai projet Crypte ;
- smoke Studio global réussi : sélection explicite, sept bindings, contrôleur IA, retrait optionnel,
  bindings/blackboard/récupération invalides refusés, dry-run, projection runtime, stale
  draft/approval, commande canonique, transaction UI, undo/redo, save/reopen, préservation des
  profils voisins, restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD des événements fallback inspectée dans
  `artifacts/verification/studio-increment/ux4-p-creature-event-ai-fhd.png` ;
- capture Forward+ FHD du contrôleur fallback inspectée dans
  `artifacts/verification/studio-increment/ux4-p-creature-controller-fhd.png` ;
- capture Forward+ FHD de la qualification sur le **vrai Gardien des Cendres** inspectée dans
  `artifacts/verification/studio-increment/ux4-p-creature-event-ai-real-crypt-fhd.png` ;
- les trois onglets, le résumé canonique et les cinq actions transactionnelles restent visibles à
  1920×1080 sans UUID, JSON ni clipping.

## ORF-CRE-035 — OnPerception

La surface édite les deux bindings perception : stimulus détecté et stimulus perdu. L’aperçu les
projette vers `OnStimulusDetected` et `OnStimulusLost`, sans créer de stimulus ni interroger un
système sensoriel moteur.

## ORF-CRE-037 — OnPhysicalAttacked

Les handlers avant attaque physique et après résolution sont authorés ensemble, ordonnés et
validés. La projection expose leur binding canonique sans infliger de dégâts ni déclencher un acteur.

## ORF-CRE-038 — OnSpellCastAt

Les étapes capacité ciblée et capacité impactée sont configurées séparément. Elles restent des
bindings typés : aucun sort, ressource, cible, script ou effet n’est exécuté dans Studio.

## ORF-CRE-040 — OnBlocked

Le handler de mouvement bloqué et l’ordre de récupération `Repath, Avoid, Wait, Abort` sont édités
sans normalisation silencieuse. Les valeurs inconnues, répétées ou hors contrat sont refusées avant
la commande.

## ORF-CRE-046 — Intelligence artificielle par profil

Le contrôleur expose son type, sa définition, un blackboard typé, les actions ordonnées et les
overrides. L’aperçu utilise réellement le résolveur canonique pour produire sept bindings et une
décision déterministe `attack`, mais ne lance ni boucle IA, ni déplacement, ni ForgeScript, ni réseau.

## Préservation et limites honnêtes

- identité, règles, authoring, interactions, mouvement, présentation, progression, loot, événements
  UX4-N/O, overrides et autres blueprints sont préservés ; seul le tail Event/IA ciblé est remplacé ;
- le parcours produit refuse une créature sans fondation au lieu d’en synthétiser une. La
  démonstration utilise une copie temporaire avec fallback canonique ; la Crypte réutilise son vrai profil ;
- aucun stimulus, attaque, capacité, mouvement, script, acteur ou transport réseau n’est exécuté ;
  le lot qualifie l’authoring, la validation, la persistance et les projections pures ;
- `ORF-CRE-033` OnSpawn est qualifié séparément par UX4-D et `ORF-CRE-034` OnConversation par UX4-J ;
  ces bindings restent des autorités distinctes et ne sont pas promus par ce lot.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreatureEventAiCompositionProfileTests|' +
  'FullyQualifiedName~CreatureEventAiCompositionFoundationServiceTests|' +
  'FullyQualifiedName~SetCreatureFoundationHandlerTests.EventAiComposition|' +
  'FullyQualifiedName=Forge.ModuleFormat.Tests.Serialization.CryptOfAshesSampleTests.SampleProject_IsValidAndContainsPrototypeActors'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-p-creature-event-ai -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-p-creature-event-ai-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-p-qualification -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-p-creature-event-ai-real-crypt-fhd.png `
  -Wait -SkipBuild
```
