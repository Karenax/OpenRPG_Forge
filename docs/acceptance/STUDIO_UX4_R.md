# Acceptation Studio — UX4-R Création assistée de boss

État local au 20 août 2026 : **fonctionnel et qualifié** pour `ORF-AI-019`. La page
**Créatures** expose un assistant de boss hors ligne et applique le lot approuvé via
`ApplyAssistedBossHandler` dans une transaction unique.

## ORF-AI-019 — Création de boss

La surface demande explicitement une zone, l’identité, les statistiques, les capacités, deux à
trois scripts de phase `.forge` réellement présents, les invocations, récompenses, citations,
règles de style, auteur et seed. L’auteur doit cocher son approbation avant le dry-run ; le même
plan approuvé ajoute atomiquement blueprint, combat, rencontre, phases, dialogue, récompenses et
document runtime.

L’aperçu appelle `AssistedBossCreationService.Generate` avec un seed déterministe et présente le
diff sémantique, le lot, l’inverse et une simulation d’équilibrage de 32 exécutions. Aucun provider
réseau, acteur, ForgeScript, combat ou moteur n’est exécuté par Studio.

## Preuves

- portail incrémental réussi en **121,46 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-r-assisted-boss.summary.json` ;
- build Studio sans avertissement et **6/6 tests ciblés** : RpgKit Domain 5 et MCP 1 ;
- smoke Studio global réussi deux fois avec sortie native 0 : sélection explicite, scripts réels,
  capacités, phases, équilibrage, approbation obligatoire, script invalide refusé, dry-run, gardes
  source/approbation, commande canonique, transaction UI, undo/redo, save/reopen, voisins
  préservés, surface restaurée et workspace temporaire libéré ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-r-assisted-boss-fhd.png` : formulaire complet,
  approbation sticky, résumé canonique, fermeture et cinq actions transactionnelles visibles à
  1920×1080, sans UUID, JSON ni clipping bloquant ;
- la matrice atteint 281 routes `functional` et 436 `planned`. Le portail reste volontairement
  `full_gate=false` : il qualifie cette route, pas la totalité du produit.

## Préservation et limites honnêtes

- le plan est lié à la révision et aux empreintes binaires du projet et du brouillon ; toute dérive
  impose un nouvel aperçu et une nouvelle approbation ;
- la preuve riche copie le module et ses scripts dans une session temporaire isolée ; projet,
  historique, état dirty, sélection et approbation source restent inchangés, puis le répertoire
  temporaire est libéré ;
- la transaction ne remplace pas les quêtes, zones, factions, contenus ni blueprints voisins ;
  undo/redo et save/reopen restituent exactement les états attendus ;
- Studio génère et simule un plan déterministe local. Il ne contacte aucun modèle distant, ne
  compile ni n’exécute les scripts, ne lance aucun combat et ne certifie aucun équilibrage live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~AssistedBossCreationServiceTests|' +
  'FullyQualifiedName~AssistedBossMcpProposesSimulatesPersistsAtomicPackageAndRevertsIt'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-r-assisted-boss -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-r-assisted-boss-fhd.png `
  -NoMatrixCheck
```
