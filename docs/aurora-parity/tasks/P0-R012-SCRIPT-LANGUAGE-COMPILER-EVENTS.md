# P0 R012 — langage, compilation et événements ForgeScript

Lot stable : `R012` / `FC-SCRIPT-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SCR-001` — Langage proche du C.
- `ORF-SCR-002` — Sources et bytecode.
- `ORF-SCR-003` — Compilateur intégré.
- `ORF-SCR-006` — Événements par objet.
- `ORF-SCR-007` — Fonction d’entrée.
- Source immuable relue intégralement pour les cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md`.
- Dépendances réévaluées : le runtime natif AngelScript 2.38, l’API ForgeScript 1.0, le cache
  source/API/compilateur, les quotas et le compilateur incrémental existent déjà.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| SCR-001 | AngelScript natif non forké, syntaxe C statiquement typée, API allowlistée et versionnée | le contrat public n’identifie pas explicitement langage, dialecte et politique d’API dans un document canonique | manifeste source versionné et API auto-documentée |
| SCR-002 | artefact bytecode, cache atomique et clé SHA-256 source/API/compilateur | aucune source `.as` canonique avec UUID, schéma, provenance et politique de distribution ; aucun round-trip documentaire exact | document source gouverné, stockage exact et cache toujours jetable |
| SCR-003 | compilation batch incrémentale, dépendances, diagnostics stables, télémétrie et conformance | le parallélisme configuré n’est pas exercé et l’éditeur Scripts n’expose ni console ni diagnostics cliquables | compilation parallèle déterministe, écran dédié et console compile/test |
| SCR-006 | événements gameplay typés et plusieurs profils spécialisés | absence d’une table générique par composant avec plusieurs handlers, priorité, condition d’activation et autorité explicite | catalogue d’événements, validation stable et ordre de dispatch déterministe |
| SCR-007 | entrée historique `void main()` exigée par la VM | aucun contrat d’entrée typé par payload événement ni validation du handler assigné | signatures typées, résolution de fonction et diagnostic avant build |

## Contrat minimal de fermeture

- définir une source `.as` canonique avec UUID, schéma, provenance, API, langage/dialecte, distribution,
  dépendances et caractère optionnel/requis ;
- sauvegarder/recharger exactement la source et ses métadonnées, sans traiter le bytecode comme source de vérité ;
- faire entrer API et compilateur dans l’empreinte et conserver diagnostics structurés et chemins sûrs ;
- compiler les unités indépendantes en parallèle de façon bornée, avec résultat réordonné de façon déterministe ;
- définir un catalogue de payloads et une table de bindings par composant, multi-handlers, priorité,
  condition d’activation, autorité et signature typée ;
- valider identité, schéma, références, API, source, handler, quotas et fonction d’entrée avec codes et
  chemins stables ; une source optionnelle absente est ignorée, une source requise est diagnostiquée ;
- fournir des transactions source/binding dry-run, apply, undo/redo et les mêmes opérations sûres via MCP ;
- remplacer l’ouverture du pipeline de build par un véritable écran Scripts avec éditeur, états vide/erreur,
  diagnostics cliquables, compilation et tests sandbox.

## Risques et tests exacts

- compatibilité du runtime natif et des scripts `.forge` historiques, sans migration destructive ;
- domaine scripting : identité, extension `.as`, dépendances, distribution, handlers multiples, ordre,
  signature, autorité et optionnel/requis ;
- compilateur/cache : clé, invalidation, parallélisme borné, diagnostics et entrée typée ;
- persistance : round-trip octet pour octet, métadonnées et écriture atomique ;
- transaction/MCP : preview, révision, apply, undo/redo/revert, lecture API/diagnostics/tests et assignation ;
- interface : éditeur dédié lisible, console, état invalide et capture inspectée ;
- intégration : tracker, backlog, docs, graphe et portails pré/post-promotion.

## Non-objectifs

- débogueur pas-à-pas, hot reload, tâches asynchrones, audio/temps/JSON ou autres fonctions SCRIPT P1/P2/P3 ;
- fork d’AngelScript, accès fichier/réseau/processus depuis les scripts ou bytecode distribué comme vérité ;
- migration massive des scripts `.forge` historiques ;
- AV7–AV10, commit ou push Git.

## Résultat livré

- `ForgeScriptSourceDocument` rend la source `.as` canonique avec UUID, schéma, provenance, API,
  distribution, dépendances, exigence runtime et bindings ; le bytecode reste un cache jetable ;
- `ForgeScriptSourceStore` écrit source et métadonnées atomiquement et les recharge sans modifier un octet
  de source ; le graphe de build classe désormais `.as` comme script ;
- le compilateur incrémental utilise réellement son parallélisme borné et le cache est protégé contre les
  compilations concurrentes d’une même clé ; ordre et diagnostics restent déterministes ;
- les contrats d’événements portent payload, composant, priorité, condition, autorité et handlers multiples,
  avec signature typée et résolution stable ; optionnel absent reste silencieux, requis absent est diagnostiqué ;
- l’historique source et MCP fournissent preview/apply/undo/redo ou revert sans mutation partielle ; MCP expose
  API, sources, compile, diagnostics, tests sandbox et assignation d’événement ;
- le workspace `Scripts` ouvre maintenant un éditeur spécialisé au lieu du pipeline de build, avec état vide,
  identité, code, console compile/test, table d’événements et diagnostics cliquables.

## Preuves exécutées

- capture inspectée : `docs/aurora-parity/evidence/r012-forgescript-editor-ui.png` (1440×880), compilation
  réussie, zéro diagnostic, deux handlers ordonnés et gouvernance lisible ;
- tests ciblés : 35 ForgeScript, 7 MCP ForgeScript et 7 tests de graphe de build ; le test MCP stdio vérifie
  explicitement les dix outils d’interop et répond en environ 2,2 secondes ;
- marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_PASS` et
  `OPENRPG_FORGE_RUNTIME_R012_SCRIPT_FOUNDATION_PASS` ;
- portail pré-promotion : compilation sans avertissement, 1 101 tests .NET, 6 tests Python généraux,
  33 tests Python asset, documentation/tracker/backlog/graphe frais, tous les smokes intégrés et
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée à `ORF-SCR-001`, `ORF-SCR-002`, `ORF-SCR-003`, `ORF-SCR-006` et `ORF-SCR-007`.
