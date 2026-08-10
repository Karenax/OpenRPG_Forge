# P0 R027 — validation canonique

Lot stable : `R027` / `FC-QA-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-QA-001` — Validation de schéma.
- `ORF-QA-002` — Validation de références.
- `ORF-QA-003` — Validation de graphes.
- `ORF-QA-004` — Validation géométrique.
- `ORF-QA-005` — Validation navigation.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/26_QA.md`.
- Dépendances BUILD, AREA, DIALOGUE, QUEST, WAYPOINT, DOOR et TRIGGER réévaluées : les documents
  versionnés, validateurs métier, navigation multi-agent et diagnostics actionnables existent déjà,
  mais aucune configuration canonique unique ne pilote les cinq familles avant build.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Schéma | Sérialiseurs et validateurs refusent les versions non supportées pour projet, zones, manifeste, sauvegarde et profils | Pas de registre/preflight commun couvrant les documents canoniques découverts ni de politique auteur dédiée | Profil UUID v1, registre versionné, scan structuré avant build, diagnostic code/chemin/UUID |
| Références | `ProjectValidator`, package/catalogue et manifeste vérifient de nombreuses UUID et types | Pas de rapport global distinguant cible absente, mauvais type, package inaccessible et incompatibilité de version sous une politique commune | Agrégation typée et déterministe, tests des quatre causes, CLI/MCP structurés |
| Graphes | Dialogue, quête, waypoint/navigation ont leurs validateurs métier ; diagnostics cliquables existent | Pas de noyau générique nœuds/arêtes ordonnés ni synthèse configurable commune | Validateur générique cycles/inaccessibles/références et règles métier conservées, diagnostics cliquables |
| Géométrie | Limites de zone, overlaps AABB, sockets et échelle implicite sont vérifiés | Échelle invalide non diagnostiquée explicitement et aucun contrat réutilisable pour polygone auto-intersecté | Codes stables pour scale/polygone, intégration au rapport et tests |
| Navigation | Reachability spawn→objets, portes, quatre agents, off-mesh, vol et bake incrémental sont présents | La politique n’est pas configurable/persistée et le résultat n’est pas réuni au preflight build | Catégorie pilotable, analyse toutes portes/agents, rapport UI/CLI/MCP, runtime requis/optionnel |
| Configuration/transactions | Quality Lab et fondation QA ont déjà UI, commande, undo/redo et persistance | Les cinq familles n’ont ni profil, ni même commande, ni round-trip exact | Profil unique, commande dry-run/apply, historique, store atomique, UI visible vide/erreur |

## Contrat minimal de fermeture

- créer une politique canonique `CanonicalValidationPolicy` UUID v1 avec provenance et cinq
  catégories activables, le schéma restant toujours requis avant build et les autres blocages étant
  configurables ;
- exécuter un preflight déterministe et non mutant avant build, avec résultats structurés et
  diagnostics stables code/chemin/UUID ;
- réutiliser les validateurs domaine existants, ajouter seulement le noyau générique de graphe et
  les contrôles géométriques réellement absents ;
- exposer la même politique et le même rapport dans Quality Lab, CLI et MCP sans shell arbitraire ;
- sauvegarder atomiquement, recharger exactement et prouver preview/apply/undo/redo ;
- conserver l’absence optionnelle sûre et le diagnostic explicite lorsqu’une catégorie requise est
  absente ou désactivée.

## Fichiers probables

- `src/Forge.Domain/Validation/` pour profil, validateur générique et diagnostics ;
- `src/Forge.Application/Validation/` pour commande/historique ;
- `src/Forge.ModuleFormat/Quality/` pour store et preflight workspace ;
- `apps/Forge.Toolset/addons/openrpg_forge/QualityLabWindow.cs` et `ForgeEditorPlugin.cs` ;
- `tools/Forge.Cli/Program.cs`, `src/Forge.Mcp/`, hôte MCP ;
- tests Domain/Application/ModuleFormat/MCP, smokes et `scripts/Verify.ps1`.

## Risques et tests exacts

- identité vide, schéma futur, provenance absente, catégorie requise désactivée ;
- fichier absent (default sûr), JSON legacy/invalid, round-trip exact et second save identique ;
- schéma inconnu, UUID dupliqué, référence absente/mauvais type/package/version ;
- graphe inaccessible/cyclique/référence typée invalide avec ordre stable ;
- échelle non finie/nulle, overlap, socket, polygone auto-intersecté ;
- reachability pour chaque agent, portes ouvertes/fermées, off-mesh et vol ;
- preview sans écriture, apply atomique, undo/redo, UI inspectée, CLI/MCP structurés ;
- build/runtime continuent de lancer les prototypes 3D et le portail global reste vert.

## Non-objectifs

- P1/P2, AV7–AV10, navigation de foule future, solveur physique exhaustif, schémas propriétaires,
  commit ou push Git.

## Clôture vérifiée

- `CanonicalValidationPolicy` et les cinq catégories passent par le profil QA existant, sa commande
  dry-run/apply, son historique undo/redo et son store JSON atomique. Le legacy sans section reprend
  un défaut sûr ; une catégorie déclarée requise mais désactivée est refusée.
- Le preflight agrège schémas, références typées, graphes génériques et métier, géométrie et navigation.
  Chaque diagnostic porte code, chemin, UUID stable, catégorie et provenance ; le rapport et son
  fingerprint sont déterministes.
- `ModulePackager` bloque les catégories requises avant toute compilation/écriture d’archive. Le
  schéma est toujours requis ; références, graphes, géométrie et navigation restent analysés mais
  optionnels par défaut pour préserver les projets legacy, puis deviennent bloquants dès que l’auteur
  les coche dans le Quality Lab.
- `forge validate` produit un JSON structuré et le marqueur
  `OPENRPG_FORGE_R027_CANONICAL_VALIDATION_CLI_PASS`. MCP expose lecture de politique, plan dry-run,
  validation canonique, build et test sans shell arbitraire.
- La capture inspectée `docs/aurora-parity/evidence/r027-qa-validation-ui.png` (1380×860) montre les cinq
  contrôles sans débordement, le schéma requis, la navigation optionnelle et un rapport à zéro diagnostic.
- Les tests ciblés Domain, Application, ModuleFormat et MCP sont verts. Les smokes réels ont émis
  `OPENRPG_FORGE_TOOLSET_R027_QA_VALIDATION_PASS` et
  `OPENRPG_FORGE_RUNTIME_R027_QA_VALIDATION_PASS`.
- Promotion strictement individuelle : `ORF-QA-001` à `ORF-QA-005` passent de `Implemented` à `Tested` ;
  aucune autre fonction n’est promue dans R027.
- Le portail intégré final construit 27 projets sans avertissement ni erreur, exécute 1 230 tests
  .NET, 6 tests Python généraux et 33 tests Python du pipeline d’assets, confirme le graphe à
  5 894 nœuds, 35 606 relations et zéro diagnostic, puis émet
  `OPENRPG_FORGE_VERIFICATION=PASS`.
