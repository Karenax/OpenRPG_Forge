# P0 R019 — politiques QA, sauvegardes et logs structurés

Lot stable : `R019` / `FC-QA-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-QA-007` — Validation de licence.
- `ORF-QA-008` — Tests unitaires du domaine, déjà `Tested` et seulement reconfirmée.
- `ORF-QA-010` — Tests headless runtime, déjà `Tested` et seulement reconfirmée.
- `ORF-QA-015` — Tests de sauvegarde.
- `ORF-QA-020` — Logs structurés.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/26_QA.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Chaque asset/package Shipping a licence et provenance approuvées, NOTICE et refus des inconnus | Distribution, packages, NOTICE et gouvernance R018 bloquent déjà avant archive | Reconfirmer la couverture asset/package complète et l’exposition auteur/CLI-MCP | Scénarios Shipping hostiles et profil QA visible |
| Tests C# des IDs, sérialiseurs, migrations, commandes, validations, règles et diffs | `ORF-QA-008` déjà `Tested`, portail à 1 162 tests | Aucun manque fonctionnel ; reconfirmer sans promotion | Portail intégré et inventaire structuré |
| Runtime headless charge module, joue scénarios, vérifie états et rapports | `ORF-QA-010` déjà `Tested`, Crypte plus module Core générique | Aucun manque fonctionnel ; reconfirmer sans promotion | Smokes runtime et rapports du portail |
| Save/load, anciennes versions, panne partielle, package absent, rollback, état déterministe | Round-trip, migration, mismatch, chunks, dépendances, rollback et rétention présents | Panne d’écriture conservant l’ancienne sauvegarde et byte-stabilité non prouvées explicitement | Tests hostiles supplémentaires et profil QA |
| Sévérité, catégories, IDs objet, ligne source, transaction, vie privée joueur et rétention | Logger serveur avec niveau, événement, corrélation et masquage secrets | Contrat typé incomplet, pas de politique canonique ni de rétention | Domaine, logger, persistance, UI et MCP |

## Contrat minimal de fermeture

- profil QA canonique UUID/schéma/provenance qui décrit validation de licence, inventaires de tests
  domaine/headless/save et politique de logs ;
- commande dry-run/apply, diff, undo/redo et persistance atomique exacte sans mutation par les tests ;
- logs typés avec sévérité, catégorie, objet, ligne, transaction, corrélation, vie privée et rétention bornée ;
- tests de sauvegarde couvrant deux versions anciennes, panne partielle, package absent, rollback et bytes
  déterministes ;
- écran auteur visible avec états vide/erreur, lecture et plan MCP filtrés, diagnostics stables ;
- marqueurs Toolset/runtime, capture inspectée, tracker/backlog/docs/graphe et portail global avant R020.

## Non-objectifs

- service cloud de logs, télémétrie distante, P1/P2, AV7–AV10, commit ou push Git.

## Preuves de fermeture

- `ORF-QA-007`, `ORF-QA-015` et `ORF-QA-020` sont promues individuellement à `Tested` ;
  `ORF-QA-008` et `ORF-QA-010`, déjà `Tested`, sont reconfirmées sans nouvelle promotion.
- Le profil `.forge/quality/foundation.json` porte UUID, schéma, provenance, politique SPDX,
  inventaires de tests domaine/headless/save et politique de logs ; son stockage est atomique,
  déterministe et UTF-8 sans BOM.
- La sauvegarde est prouvée sur les formats `1.1.0` et `1.2.0`, le round-trip courant, les bytes
  déterministes et l'échec de remplacement qui conserve l'ancienne sauvegarde sans fichier temporaire.
- Le logger applique sévérité et catégories typées, corrélation, UUID objet, ligne source,
  transaction, masquage/hash des identifiants joueur et rétention par durée/volume.
- Commande dry-run/apply, diff, undo/redo, lecture et plan MCP sans écriture, UI auteur avec états
  vide/erreur et contrôles de sévérité, confidentialité et rétention sont validés.
- Tests ciblés verts : Domain 2/2, Application 2/2, ModuleFormat 9/9, Network 6/6 et MCP 1/1.
- Marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R019_QA_FOUNDATIONS_PASS` et
  `OPENRPG_FORGE_RUNTIME_R019_QA_FOUNDATIONS_PASS`.
- Preuve visible inspectée : `docs/aurora-parity/evidence/r019-qa-foundation-ui.png` en 1380×860,
  montrant le profil, les garanties et les commandes auteur sans rognage du socle QA.
- Portail global : 27 projets, zéro avertissement/erreur, 1 173 tests .NET, 6 tests Python généraux,
  33 tests du pipeline d'assets, tracker/backlog/docs frais, graphe à 5 754 nœuds et 34 550 relations,
  puis `OPENRPG_FORGE_VERIFICATION=PASS` avant l'ouverture de R020.
