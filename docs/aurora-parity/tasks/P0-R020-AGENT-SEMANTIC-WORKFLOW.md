# P0 R020 — contexte et workflow sémantique des agents

Lot stable : `R020` / `FC-AI-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-AI-002` — Contexte structuré du projet.
- `ORF-AI-004` — Graph de références.
- `ORF-AI-005` — Outils sémantiques.
- `ORF-AI-006` — Plan avant mutation.
- `ORF-AI-007` — Dry-run.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/24_AI.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Ressources structurées projet, monde, zones, entités, dialogues, quêtes, scripts, assets, diagnostics et sélection | Résumé projet, lectures spécialisées, catalogue assets et diagnostics MCP existent | Pas de paquet de contexte unique, borné, versionné et sans lecture brute de dossiers | Projection structurée couvrant les dix surfaces, sélection résolue et tests de bornes |
| Graphe inverse et rapports d’impact avec références typées | `CreatorReferenceIndex` alimente renommage sûr et diagnostics | Pas d’énumération publique, d’identité de graphe, d’arêtes UUID ni de requête d’impact MCP | Graphe déterministe, reverse refs, portée/profondeur bornées et mini-carte UI |
| Outils MCP par commandes métier | De nombreux outils spécialisés passent déjà par les handlers de domaine | Aucun catalogue sémantique canonique expliquant commande, permission et cible | Catalogue versionné, validé et exposé avec commandes `area.add_entity`, `dialogue.connect`, etc. |
| ChangePlan avec but, commandes, hypothèses, objets, risques et tests | Les ateliers assistés possèdent des plans spécialisés | Pas de contrat transversal unique ni de validation complète des six rubriques | Plan UUID/transaction/révision/permission, diagnostics localisés et aperçu UI/MCP |
| Validation de batch et diff sans écriture | Les commandes spécialisées ont des dry-runs et diffs | Pas de dry-run transversal vérifiant révision, outils, cibles et absence d’écriture | Empreinte avant/après, diff déterministe, refus stale/permission/outil/cible |

## Contrat minimal de fermeture

- extension canonique du profil assistant avec UUID, schéma, provenance, surfaces de contexte,
  politique de graphe, catalogue d’outils sémantiques et exigences plan/dry-run ;
- paquet de contexte MCP unique et borné, construit depuis les objets canoniques et métadonnées
  gouvernées, jamais par ingestion brute d’un dossier ;
- graphe de références déterministe avec nœuds/arêtes UUID, types, sens inverse et impact borné ;
- `StructuredChangePlan` complet et dry-run de batch à révision courante retournant un diff sans
  écrire ni changer la révision ;
- même commande de profil pour UI et domaine, plan préalable obligatoire, permission, undo/redo et
  round-trip exact ;
- UI visible avec états vide/erreur, configuration de bornes, contexte, graphe, outils, plan, diff,
  provenance et coût nul/offline ;
- tests Domain/Application/ModuleFormat/MCP, marqueurs Toolset/runtime, capture visible puis portail.

## Risques et tests exacts

- contexte : limite nulle/excessive, sélection inconnue, ordre non déterministe et fuite de contenu brut ;
- graphe : cible absente, UUID d’arête instable, profondeur/volume excessifs et impact inverse incorrect ;
- plan : UUID vide, rubrique absente, doublons, permission insuffisante, révision obsolète et outil inconnu ;
- dry-run : empreinte de projet et révision inchangées, diff stable et aucune écriture workspace ;
- persistance : ancien profil sans extension migré par défaut, JSON déterministe et UTF-8 sans BOM.

## Non-objectifs

- transaction atomique et audit IA détaillés de R021, prompt injection/offline de R022 ;
- P1/P2, AV7–AV10, exécution générique d’une commande arbitraire, commit ou push Git.

## Fermeture et preuves

Les cinq fonctions du lot ont été auditées et promues individuellement à `Tested` :

- `ORF-AI-002` : paquet de contexte v1 borné couvrant les dix surfaces canoniques, sans lecture
  brute des scripts ;
- `ORF-AI-004` : graphe déterministe de références typées et requête d'impact inverse bornée ;
- `ORF-AI-005` : catalogue canonique de cinq outils sémantiques avec cible et permission ;
- `ORF-AI-006` : `StructuredChangePlan` validé avec but, commandes, hypothèses, objets, risques et
  tests ;
- `ORF-AI-007` : dry-run à révision courante, diff stable et empreinte avant/après identique.

Preuves reproductibles :

- tests ciblés : Domain 5/5, Application 3/3, ModuleFormat 4/4 et MCP 1/1 ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_R020_AGENT_WORKFLOW_PASS` ;
- smoke runtime : `OPENRPG_FORGE_RUNTIME_R020_AGENT_WORKFLOW_PASS` ;
- capture inspectée : `docs/aurora-parity/evidence/r020-agent-semantic-workflow-ui.png`,
  1600×960, états explicites et commandes entièrement visibles ;
- tracker/backlog/docs régénérés : 292 fonctions `Tested`, 30 lots P0 ouverts ;
- graphe déterministe : 5 784 nœuds, 34 737 relations, zéro diagnostic ;
- portail intégré : 27 projets construits sans avertissement ni erreur, 1 178 tests .NET,
  6 tests Python généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.
