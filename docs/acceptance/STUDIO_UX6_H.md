# Acceptation Studio — UX6-H APIs monde ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes ForgeScript.
UX6 atteint **36/84** ; le portail de ce lot reste incrémental (`full_gate=false`).

La page **ForgeScript · API monde R098** réutilise sans duplication la source, l’historique,
l’aperçu sec, la compilation, la sandbox et l’écriture atomique UX6-F. Elle génère un bloc versionné
à relire puis à insérer explicitement dans le brouillon courant. Les sélecteurs affichent les noms
humains des objets, acteurs, quêtes, dialogues et factions du projet ; leurs identités techniques
restent internes. Si une famille manque, la surface affiche un état vide et renvoie vers sa page
d’auteur au lieu de synthétiser du contenu dans le module ouvert.

<a id="orf-scr-012"></a>
## ORF-SCR-012 — API inventaire

Le scénario choisit un propriétaire, un destinataire, un objet réel et une quantité. La simulation
exécute transfert autoritaire, contrôle de capacité, query et audit sur un état copié, puis prouve
le rollback intégral d’une demande cliente refusée. Aucun inventaire du projet ou acteur Godot
n’est modifié.

<a id="orf-scr-013"></a>
## ORF-SCR-013 — API journal et quêtes

Quête, portée d’instance, états source/cible, objectif et progression proviennent du catalogue
ouvert. La transition conserve cause, historique et révision sur une projection immuable ; aucun
journal joueur ni sauvegarde runtime n’est écrit par l’aperçu.

<a id="orf-scr-014"></a>
## ORF-SCR-014 — API dialogues

Un graphe réel et deux acteurs distincts alimentent démarrage, participants, variables, complétion
et interruption sûre. La session de dialogue est temporaire : aucune conversation live, caméra,
voix ou réplication réseau n’est lancée.

<a id="orf-scr-015"></a>
## ORF-SCR-015 — API factions et réputation

Acteur, faction active, delta, adhésion et portée sont explicites. Forgotten Forest ne possède pas
de faction : la preuve riche ajoute donc **Gardiens de la forêt** uniquement dans la copie
temporaire, tandis que le parcours produit normal conserve et explique cet état vide. La simulation
ne change ni appartenance ni réputation du projet source.

<a id="orf-scr-016"></a>
## ORF-SCR-016 — API perception

Observateur, cible, sens, distance, portée, ligne de vue, signal et seed produisent une décision,
une confiance et une trace déterministes. La même requête rejouée donne le même résultat. Aucun
raycast, audio, acteur, caméra ou moteur Godot n’est exécuté.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **13/13** — RPG Kit Domain/Application **11**, runtime natif ForgeScript **1**,
  exposition MCP sandboxée **1** ;
- portail incrémental : **166,56 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ;
- graphe Roslyn régénéré puis contrôlé intégralement : **10 458 nœuds**, **77 409 relations**,
  **0 diagnostic**, fraîcheur et déterminisme complets ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_H_PASS` : sélection explicite, autorité UX6-F partagée,
  catalogues humains, source revue, cinq API, résolution optional/required, rollback et handle
  périmé, exécution native, dry-run, compilation, sandbox, approbation obsolète, apply/undo/redo,
  persistance, voisins, transaction UI, libération de l’isolat, source et surface restaurées ;
- capture Forward+ FHD inspectée :
  `artifacts/verification/studio-increment/ux6-h-script-world-apis-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, cinq onglets directement visibles, source,
  sélecteurs, diagnostics, console et six actions transactionnelles lisibles sans clipping ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-h-script-world-apis.summary.json`.

## Préservation et limites honnêtes

La preuve copie le projet et une source canonique dans un workspace temporaire, ajoute une faction
seulement quand la source réelle n’en contient aucune, puis exerce construction, insertion,
simulation, compilation, sandbox, aperçu, apply, undo, redo et réouverture. Projet, historique,
révision, journal, dirty, zone active, approbation et surface source sont comparés avant/après ;
l’isolat est supprimé.

Les cinq appels natifs s’exécutent réellement dans la sandbox bornée. Les résultats métier affichés
restent toutefois des projections en mémoire : aucun objet gameplay, inventaire, quête, dialogue,
faction, perception moteur, fichier du module ou transport réseau n’est muté avant l’action
explicite **Appliquer et enregistrer**, qui n’écrit que la source `.as` et sa métadonnée via UX6-F.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ScriptWorldServiceTests|' +
  'FullyQualifiedName~ForgeScriptWorldApiRuntimeAdapterTests|' +
  'FullyQualifiedName~Inventory_quest_dialogue_faction_and_perception_apis_emit_typed_world_intents|' +
  'FullyQualifiedName~P1_world_apis_are_versioned_and_execute_without_external_access'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-h-script-world-apis -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-h-script-world-apis-forward-plus-fhd.png
```
