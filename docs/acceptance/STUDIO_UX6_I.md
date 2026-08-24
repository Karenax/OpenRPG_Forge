# Acceptation Studio — UX6-I Monde, mouvement, utilitaires et sandbox ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes ForgeScript.
UX6 atteint **41/84** ; ce lot reste un incrément local (`full_gate=false`).

La page **ForgeScript · monde, mouvement et utilitaires R013** réutilise l’autorité de source,
l’historique, le compilateur, la sandbox et l’écriture atomique UX6-F. Elle sépare le code revu de
cinq onglets humains — **Zones et objets**, **Mouvement**, **Math et chaînes**,
**Variables locales** et **Sandbox**. Les zones, tags et blueprints proviennent du module ouvert ;
aucun UUID n’est affiché et aucun contenu n’est synthétisé dans le parcours produit.

<a id="orf-scr-009"></a>
## ORF-SCR-009 — API zones et objets

Une zone réelle, deux tags humains distincts et un blueprint existant alimentent `current_area`,
les requêtes bornées, la lecture de position, l’apparition puis la destruction. Les intentions sont
traduites par `ForgeScriptWorldCommandService` sur une copie de `AreaDefinition`, avec aperçu,
application, annulation et rétablissement. Aucun nœud Godot ni projet source n’est modifié.

<a id="orf-scr-017"></a>
## ORF-SCR-017 — API mouvement

Mode marche/course/vol/nage, destination, vélocité et distance de suivi alimentent les tâches
opaques `move_to`, `face_towards`, `follow`, `set_velocity`, `teleport` et `cancel_movement`.
Le résultat est appliqué au modèle copié ; aucun acteur, pathfinding, physique ou réseau live n’est
exécuté dans Studio.

<a id="orf-scr-021"></a>
## ORF-SCR-021 — API math et chaînes

Vecteurs, quaternion identité, longueur, produit scalaire, formatage invariant, longueur Unicode et
expression régulière bornée s’exécutent réellement dans le runtime natif. Les motifs, entrées et
précision sont validés avant compilation ; les dépassements restent bloquants.

<a id="orf-scr-022"></a>
## ORF-SCR-022 — Variables locales

Les types entier 64 bits, nombre réel, chaîne UTF-8, `EntityRef` et `Vec3` sont écrits puis relus sur
le sujet copié. Les identités restent opaques et aucune variable d’un acteur runtime ou sauvegarde
joueur n’est persistée par la simulation.

<a id="orf-scr-027"></a>
## ORF-SCR-027 — Sandbox sans fichier ni réseau

Le manifeste versionné refuse avant compilation les fichiers, sockets, processus, pointeurs natifs,
réflexion, Godot et .NET. Une boucle abusive atteint le quota d’instructions ; le même runtime doit
ensuite exécuter à nouveau un script valide, ce qui prouve une récupération sans empoisonnement.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **18/18** — commande monde Application **2**, runtime/manifeste Scripting **9**,
  exposition MCP **7** ;
- portail incrémental : **197,50 s**, build, 18/18 tests, smoke global, capture Forward+ FHD et
  contrôle des 717 lignes de matrice ;
- graphe Roslyn régénéré et contrôlé intégralement : **10 471 nœuds**, **77 556 relations**,
  **0 diagnostic**, fraîcheur et déterminisme complets ;
- smoke global `OPENRPG_FORGE_STUDIO_UX6_I_PASS` : sélection explicite, autorité UX6-F partagée,
  catalogues humains, source revue, monde, mouvement, utilitaires, locaux typés, capability interdite,
  quota et récupération, dry-run, commande apply/undo/redo, intention invalide, révision obsolète,
  compilation, sandbox, approbation obsolète, persistance, projet complet préservé, transaction UI,
  isolat libéré, source et surface restaurées ;
- capture Forward+ FHD inspectée :
  `artifacts/verification/studio-increment/ux6-i-world-movement-utilities-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, cinq onglets visibles, code, références humaines,
  diagnostics, console et six actions transactionnelles lisibles sans clipping ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-i-world-movement-utilities.summary.json`.

## Préservation et limites honnêtes

La preuve copie le projet réel Crypte des Cendres et une source canonique dans un workspace
temporaire, puis exerce construction, insertion, simulation, compilation, sandbox, aperçu, apply,
undo, redo et réouverture. Projet sérialisé complet, historique, révision, journal, dirty, zone
active, approbation et surface source sont comparés avant/après ; le workspace temporaire est
supprimé.

Le runtime natif exécute réellement les appels autorisés et les quotas. Les commandes monde et
mouvement ne ciblent cependant qu’une projection C# en mémoire : aucun acteur Godot, navigation,
collision, fichier du module, sauvegarde, processus, réseau ou autorité serveur live n’est invoqué.
L’action **Appliquer et enregistrer** écrit seulement la source `.as` et sa métadonnée via UX6-F.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptWorldCommandServiceTests|' +
  'FullyQualifiedName~NativeForgeScriptRuntimeTests.World_movement_utilities_and_local_variables_execute_through_the_allowlist|' +
  'FullyQualifiedName~ForgeScriptCapabilityManifestTests|' +
  'FullyQualifiedName~ForgeScriptAgentServiceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-i-world-movement-utilities -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-i-world-movement-utilities-forward-plus-fhd.png
```
