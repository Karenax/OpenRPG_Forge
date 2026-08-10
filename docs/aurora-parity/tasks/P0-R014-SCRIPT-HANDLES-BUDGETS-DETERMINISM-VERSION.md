# P0 R014 — handles, budgets, déterminisme et version ForgeScript

Lot stable : `R014` / `FC-SCRIPT-P0-03` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SCR-028` — Handles opaques.
- `ORF-SCR-029` — Budget d’instructions.
- `ORF-SCR-030` — Budget mémoire.
- `ORF-SCR-031` — Déterminisme.
- `ORF-SCR-036` — Version d’API.
- Source immuable relue intégralement pour les cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md`.
- Dépendances réévaluées : R012 fournit source/compilation/événements/transactions ; R013 fournit
  allowlist, monde/mouvement/utilitaires, intents canoniques et récupération après quota ; BUILD fournit
  l’empreinte API du cache ; MULTI impose ordre stable et autorité serveur.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| SCR-028 | typedefs `EntityRef`, `AreaRef`, `MovementTaskRef` opaques et pont canonique sans pointeur | valeurs `uint64` brutes sans type natif vérifié, génération, invalidation ni détection stale ; `AssetId`/`EffectHandle` absents | registre générationnel typé managé/natif, validation à chaque appel et rejet stale/type |
| SCR-029 | callback instruction, timeout, profondeur et récupération après abort | quota unique par exécution, sans agrégation handler/tick/module, annulation explicite ni télémétrie retournée | profil canonique, session agrégée, cancellation et diagnostic/compteurs structurés |
| SCR-030 | limites source/chaîne/regex/spawn et buffers bornés | aucune interception des allocations VM, budget contexte/module, pic mémoire ou diagnostic stable | allocateur VM suivi, limites mémoire, télémétrie et runtime sain après dépassement |
| SCR-031 | ordre de résultats/handlers stable, serveur autoritaire et clocks/outils déterministes hors VM | aucune API RNG/clock ForgeScript seedée ni politique persistée ; pas de répétition bytecode→événements prouvée | seed/tick/horloge simulée, requêtes stables et test de replay identique |
| SCR-036 | API native `1.0.0`, cache lié à la version, manifest/get_api et source déclarant l’API | aucune négociation SemVer/capabilities, dépréciation, notes de migration ou matrice de compatibilité | contrat de versions, négociation, diagnostics, migration et tests de compatibilité |

## Contrat minimal de fermeture

- encoder type, génération et slot dans tout handle natif ; enregistrer/résoudre/invalider sans pointeur et
  refuser handle nul, stale ou du mauvais type à chaque appel ;
- définir une politique d’exécution canonique UUID/schéma/provenance/API avec budgets handler, tick, module,
  timeout, récursion, spawn, mémoire contexte/module, seed, tick et autorité ;
- mesurer instructions, pic mémoire et durée, agréger par tick/module et supporter annulation explicite ;
- intercepter les allocations VM, borner chaînes/collections exposées et produire un diagnostic stable sans
  empoisonner l’exécution suivante ;
- exposer RNG seedé et horloge de simulation, conserver ordre stable et autorité serveur ;
- définir SemVer, versions supportées, capacités, dépréciations et migrations ; négocier avant compilation ;
- rendre politique, télémétrie et compatibilité visibles dans l’éditeur existant sans JSON ni fichier généré ;
- conserver transactions source/configuration, round-trip, preview/apply/undo/redo et MCP R012–R013.

## Risques et tests exacts

- ABI native et compatibilité des scripts existants après encodage des handles ;
- use-after-release, génération réutilisée, confusion Entity/Area/Asset/Effect et handle forgé ;
- allocations VM au chargement/exécution, dépassement mémoire et libération complète ;
- quotas handler/tick/module, timeout, cancellation et reprise saine ;
- deux exécutions même seed/tick doivent produire exactement événements et télémétrie fonctionnelle ;
- versions exactes/compatibles/incompatibles, capacité manquante et API dépréciée ;
- capture inspectée, tests ciblés, marqueurs intégrés et portails pré/post-promotion.

## Non-objectifs

- hot reload, débogueur, tests/profilage avancés ou fonctions SCRIPT P1/P2/P3 ;
- protocole multijoueur complet, distribution de bytecode entre plateformes ou accès natif arbitraire ;
- AV7–AV10, commit ou push Git.

## Fermeture réalisée

- `SCR-028` : registre managé et registre natif encodent type, génération et slot pour `EntityRef`, `AreaRef`,
  `AssetId`, `EffectHandle` et `MovementTaskRef`. Les API résolvent le type et la génération avant emploi,
  invalident les handles libérés et ne transportent aucun pointeur moteur.
- `SCR-029` : `ForgeScriptExecutionPolicy` porte les budgets handler/tick/module, timeout, récursion et spawn ;
  `ForgeScriptExecutionSession` agrège les instructions, l’hôte expose une annulation thread-safe et retourne
  une télémétrie instructions/durée/mémoire avec diagnostics stables et reprise saine.
- `SCR-030` : l’allocateur AngelScript est instrumenté par contexte. Le dépassement est détecté au chargement
  ou entre instructions, provoque un abort contrôlé `quota.memory_limit_exceeded`, alimente le pic mémoire et
  ne fait plus tomber ni empoisonner le processus ; la limite module est appliquée par la session.
- `SCR-031` : `random_int`, `simulation_tick` et `simulation_time_ms` n’utilisent ni horloge système ni source
  aléatoire implicite. Seed, tick, temps et autorité serveur sont persistés dans la politique ; deux exécutions
  identiques produisent la même séquence d’événements.
- `SCR-036` : parseur SemVer, compatibilité `1.0`/`1.0.0`, négociation de capacités, diagnostic d’incompatibilité,
  dépréciation `variable` vers `local_set_string`, migration et ressources MCP générées sont effectifs.
- L’éditeur présente trois profils sans JSON, leur résumé instruction/mémoire/seed/tick/autorité, la télémétrie
  du sandbox et les commandes prévisualiser/appliquer/annuler/rétablir avec persistance atomique.

## Preuves de fermeture

- tests ciblés : 51 `Forge.Scripting.Tests`, 284 `Forge.Application.Tests` et 56 `Forge.Mcp.Tests`, tous verts ;
- abus : type erroné, handle stale, quota mémoire, instruction, timeout, annulation et agrégat, avec exécution
  saine immédiatement après refus ;
- runtime : `OPENRPG_FORGE_RUNTIME_R014_SCRIPT_SAFETY_POLICY_PASS` puis
  `OPENRPG_FORGE_RUNTIME_SMOKE_PASS` ;
- outil auteur : `OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_PASS` ;
- capture inspectée : `docs/aurora-parity/evidence/r014-forgescript-safety-policy-ui.png` en 1440 × 880,
  six capacités visibles, politique standard lisible et aucun contrôle rogné ;
- documentation générée : `docs/generated/forgescript-api.md` ;
- graphe : 5 565 nœuds, 33 140 relations, zéro diagnostic ;
- portail pré-promotion : 1 120 tests .NET et 39 tests Python, packaging/smokes/MCP/sauvegarde verts,
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les cinq fonctions du lot R014 peuvent seules passer de `Researched` à `Tested`. Aucun statut P1/P2/P3,
aucune fonction AV7–AV10 et aucun autre lot ne sont promus par cette décision.
