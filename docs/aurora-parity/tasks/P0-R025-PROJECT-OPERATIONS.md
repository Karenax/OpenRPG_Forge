# P0 R025 — opérations de projet déterministes

Lot stable : `R025` / `FC-PROJECT-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-PROJ-006` — Événement de chargement du module.
- `ORF-PROJ-019` — Variables de projet.
- `ORF-PROJ-020` — Contenu personnalisé ordonné.
- `ORF-PROJ-021` — Scripts mis en cache.
- `ORF-PROJ-024` — Paramètres par cible.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/02_PROJECT.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| `ProjectLoaded` puis `WorldStarted`, séparés par contexte Editor/Client/Server, ordre stable, liaison ForgeScript | `RuntimeLifecycleCoordinator` émet des événements v1 UUID, chemin, cause, autorité, provenance et séquence ; les sources ForgeScript possèdent des liaisons ordonnées et validées | Le tracker cite à tort l’absence de bus/persistance, non demandés ; il manque une preuve R025 couvrant toutes les autorités et l’intégration au contrat de source | Tests par autorité, ordre stable, dry-run sans mutation, liaison visible et marqueur runtime R025 |
| Blackboard global typé, scopes session/save/player/party/server, inspection et réplication bornée | Définitions canoniques v1, validation, persistance, UI, commande, handles sûrs, snapshots et deltas existent | Le runtime autorise encore toute clé non déclarée même quand un schéma est présent | Compatibilité legacy uniquement si aucune définition ; rejet déterministe des clés inconnues dès qu’un schéma existe ; tests runtime/Application |
| Packages versionnés, ordre explicite, conflits, lock et provenance | `ContentPackageCatalog`, resolver, compilateur, lock store, UI, commande, ForgeScript et MCP couvrent ces contrats depuis R016 | Preuves du tracker encore limitées à l’ancien manifest | Test d’intégration R025 sur résolution répétable, lock et provenance ; preuves tracker actualisées |
| Cache bytecode indexé par hashes source/API/compilateur et build reproductible | `ForgeScriptArtifactCache` vérifie identité, intégrité, invalidation et écriture atomique ; build/runtime/Creator le consomment | Cache distant non requis ; il manque une preuve R025 multi-racines et une synthèse visible du contrat | Compilation identique dans deux caches, corruption/invalidation testées, état visible et marqueurs R025 |
| Profils Editor/Client/DedicatedServer/Test/Shipping, secrets séparés, validation | Configuration v1 à cinq profils, UI transactionnelle, conformité, round-trip et pipeline existent | Les secrets réels ne doivent justement pas entrer dans le document canonique ; preuve transversale R025 à ajouter | Cinq références de secrets distinctes, validation par cible, dry-run/undo/redo, persistance et synthèse visible |
| Contrat commun UI/commande/runtime/ForgeScript/MCP/validation/undo | Chaque sous-système possède déjà sa frontière canonique et ses tests spécialisés | Aucun écran ne résume l’état opérationnel des cinq fonctions et les preuves restent dispersées | Vue de synthèse non mutante dans la configuration du module, capture inspectée et test d’intégration ciblé |

## Contrat minimal de fermeture

- préserver les modèles et commandes existants, sans second modèle concurrent ;
- rendre le blackboard strict lorsqu’un schéma de variables est déclaré, avec compatibilité explicite et bornée
  pour les anciens projets sans définition ;
- ajouter une lecture de préparation opérationnelle déterministe réunissant cycle de vie, blackboard, packages,
  cache et cibles, sans accès fichier arbitraire ni mutation ;
- exposer cette synthèse dans l’interface existante de configuration avec états vide/valide/erreur ;
- prouver les cinq contrats par tests ciblés, capture 1440×980 inspectée, marqueurs Toolset/runtime R025,
  puis portail intégré.

## Fichiers probables

- `apps/Forge.Runtime/Runtime/GameRoot.cs` ;
- `src/Forge.Application/Modules/ProjectBlackboardScriptService.cs` et tests associés ;
- `apps/Forge.Toolset/addons/openrpg_forge/ModuleConfigurationWindow.cs` ;
- tests Domain/Application/Scripting/ModuleFormat/MCP déjà spécialisés ;
- `scripts/Verify.ps1`, trackers et documentation générée seulement après preuves vertes.

## Risques et tests exacts

- projet legacy sans définition : écriture historique encore acceptée ; projet schématisé : clé inconnue refusée ;
- scope/replication/handle périmé, variable requise invalide et round-trip exact ;
- ordre lifecycle identique en Editor, Client, Server et dry-run sans état modifié ;
- package manquant, cycle, conflit scellé, contrainte de version, lock stable et provenance conservée ;
- cache identique entre deux racines, hit local, source/API/compilateur modifié et artefact corrompu ;
- cinq cibles uniques, références de secrets distinctes, invalidité refusée avant build ;
- synthèse visible sans écriture, capture sans débordement bloquant, marqueurs et portail global.

## Non-objectifs

- bus réseau d’événements, persistance d’un journal d’événements, cache distant, stockage de secrets réels,
  paramètres plateforme P1/P2, AV7–AV10, commit ou push Git.

## Clôture R025

- `ORF-PROJ-006`, `ORF-PROJ-019`, `ORF-PROJ-020`, `ORF-PROJ-021` et `ORF-PROJ-024` ont été
  relus puis promus individuellement à `Tested` ; aucune autre ligne P0, P1 ou P2 n’a été promue.
- Le runtime utilise désormais le mode de compatibilité blackboard seulement lorsque le module ne
  déclare aucune variable ; dès qu’un schéma existe, toute clé inconnue produit un refus déterministe.
- Les preuves existantes de cycle de vie multi-autorité, packages ordonnés avec lock/provenance,
  cache reproductible et cinq profils à secrets séparés ont été réauditées et reliées au tracker.
- La fenêtre Configuration expose une synthèse non mutante des cinq contrats. La première capture a
  révélé un pied masqué à 840 px ; la hauteur a été corrigée puis la capture finale
  `docs/aurora-parity/evidence/r025-project-operations-ui.png` (1440×920) a été inspectée : synthèse,
  trois colonnes et actions sont entièrement visibles, sans débordement bloquant.
- Tests ciblés : 17 Domain, 6 Application, 4 Scripting, 9 ModuleFormat et 3 MCP, tous verts.
- Smokes : `OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_PASS` et
  `OPENRPG_FORGE_RUNTIME_R025_PROJECT_OPERATIONS_PASS` présents.
- Tracker/backlog/docs régénérés ; graphe final : 5 868 nœuds, 35 425 relations, zéro diagnostic.
- Portail intégré : 27 projets, zéro avertissement/erreur, 1 218 tests .NET, 6 tests Python généraux,
  33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.
