# P1 R055 — Audio, événements, carte, visibilité et LOD de zone

Lot stable : `R055` / `FC-AREA-P1-03` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AREA-021` — audio de zone ;
- `ORF-AREA-024` — événement utilisateur de zone ;
- `ORF-AREA-029` — carte et brouillard de guerre ;
- `ORF-AREA-033` — occlusion et visibilité ;
- `ORF-AREA-034` — LOD par zone et asset.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/03_AREA.md`.

## Audit avant fermeture

| Critère source | Présent avant R055 | Limite réelle fermée par R055 |
|---|---|---|
| audio | musiques jour/nuit/combat, ambiance, réverbération, transitions, couches adaptatives et orchestration | identité/provenance auteur, snapshots de mixage transactionnels et vue runtime typée ; les zones de réverbération existantes restent partagées |
| événements | UUID, nom, schéma payload et script persistants | scope `area.<slug>`, version API, abonnements, payload runtime, dispatch ForgeScript et fallback requis/optionnel |
| carte | activation, projection, étages, pins et drapeau fog of war | génération de plans 2D/3D, exploration joueur/parti/monde par cellules, état révélé, API de révélation et pins persistants |
| visibilité | estimation de secteurs occupés | rooms, portails conditionnels, occluders transformés, culling, streaming par distance et visualiseur de cellules |
| LOD | comptage proche/moyen/lointain | politique importée/générée/hybride, seuils, budgets, overrides asset, HLOD, cross-fade et mesure de popping |
| intégration auteur | écrans audio et carte séparés, commande auteur existante | écran R055 en trois onglets avec états vide/erreur/aperçu et mutation par la même commande canonique |
| agents/runtime/build | fondations partielles consommées séparément | lecture filtrée, plan dry-run/apply/revert, plan de rendu, persistance, undo/redo et refus pré-build localisé |

## Contrat livré

- `AreaExperienceAuthoringProfile` canonique UUID/schéma/provenance ;
- snapshots audio reliés aux profils musique, ambiance, couches adaptatives et réverbération existants ;
- bus de zone namespacé, payload versionné et abonnements ForgeScript ;
- carte 2D/3D par étages avec cellules d'exploration, brouillard et révélation par joueur/parti/monde ;
- rooms, portails, occluders, streaming et visualiseur de cellules ;
- politique LOD/HLOD mesurable avec budgets, overrides asset et contrôle de popping ;
- transaction UI/Application, persistance, historique, ForgeScript, MCP et préflight package ;
- absence optionnelle sûre et absence requise diagnostiquée.

## Preuves produites

- neuf tests ciblés R055 : Domain 3, Application 2, EngineAdapter 1, ModuleFormat 2 et MCP 1 ;
- capture 1320×820 inspectée `docs/aurora-parity/evidence/r055-area-experience-ui.png` ;
- marqueurs Toolset et runtime `R055_AREA_EXPERIENCE_PASS` obtenus avec l'exécutable Mono non-console ;
- round-trip projet/workspace, plan de rendu 2D/3D, refus pré-build et portail global reproductible.

## Non-objectifs

- fonctions AREA P1 du lot R056 et suivants ;
- fonctions P2, lots Asset/Map AV7–AV10, commit ou push.
