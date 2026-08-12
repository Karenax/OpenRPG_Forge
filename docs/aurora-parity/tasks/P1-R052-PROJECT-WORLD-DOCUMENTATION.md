# P1 R052 — Événements custom, horloge et documentation joueur

Lot stable : `R052` / `FC-PROJECT-P1-02` — trois fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PROJ-014` — événement utilisateur global ;
- `ORF-PROJ-016` — temps et calendrier du monde ;
- `ORF-PROJ-022` — description et instructions joueur.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/02_PROJECT.md`.

## Audit avant fermeture

| Critère source | Présent | Limite réelle R052 |
|---|---|---|
| événement custom typé | `CustomEventSchema` et `CustomWorldEventBus` validaient namespace, version et payload | profil module persistant, binding ForgeScript, autorité, UI, runtime optionnel/requis et MCP filtré |
| temps du monde | `WorldClockProfile`, fuseaux et franchissements astronomiques existaient en mémoire | configuration auteur, identité/validation, sauvegarde/reprise et runtime réel |
| pages joueur | nom, résumé, description et crédits localisés existaient | huit pages distinctes : synopsis, installation, règles, contrôles, crédits, licences, avertissements, compatibilité |
| transaction | commandes politiques et configuration module existaient | préserver les extensions R051/R052, même commande pour UI/MCP, diff, audit, undo/redo |
| build | validation projet et export existaient | diagnostics stables avec chemin avant toute écriture du package |

## Contrat livré

- `ProjectWorldProfile` canonique UUID/schéma/provenance avec événements custom et `WorldClockProfile` ;
- payloads `string`, `int`, `float`, `bool` et `guid`, handler ForgeScript sûr et autorité serveur ;
- vitesse, calendrier, pause, minute initiale, fuseaux par zone et événements astronomiques ;
- `WorldClockSnapshot` conservé dans `.orpsave` format 1.4 avec migration legacy 1.3 ;
- huit pages joueur localisées persistantes et bornées à 65 536 caractères chacune ;
- onglets Toolset « Monde & calendrier » et pages joueur, avec états vide/erreur ;
- lectures MCP filtrées et plans dry-run/apply/revert sans accès direct aux fichiers ;
- fallback runtime optionnel silencieux et absence requise diagnostiquée.

## Preuves produites

- huit tests ciblés R052 : Domain 3, Application 1, ModuleFormat 3 et MCP 1 ;
- round-trip courant/legacy du projet, de la sauvegarde et du package ;
- refus pré-build `project.world.*` et `project.documentation.page_too_large` ;
- captures inspectées `r052-project-world-ui.png` et `r052-project-docs-ui.png` ;
- marqueurs Toolset `R052_PROJECT_WORLD_PASS`, `R052_PROJECT_DOCS_PASS` et runtime
  `R052_PROJECT_WORLD_PASS` obtenus avec l’exécutable Mono non-console ;
- tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` vert.

## Non-objectifs

- fonctions AREA P1 de R053 et suivantes ;
- fonctions P2, AV7–AV10, commit ou push.
