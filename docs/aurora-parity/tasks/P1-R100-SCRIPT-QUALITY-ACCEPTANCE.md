# R100 — Acceptation qualité ForgeScript

## Périmètre

`R100 / FC-SCRIPT-P1-04` ferme exactement cinq fonctions P1 :

- `ORF-SCR-034` — tests unitaires ForgeScript ;
- `ORF-SCR-035` — profilage de scripts ;
- `ORF-SCR-037` — documentation générée ;
- `ORF-SCR-038` — analyse statique ;
- `ORF-SCR-040` — sérialisation d’état script.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md`, lignes 2585–2660,
2661–2736, 2813–2888, 2889–2964 et 3041–3116.

## Réduction des dépendances

QA possède le portail et les rapports, AI consomme la référence générée, GAMEPLAY fournit les
composants persistants et EE motive les métriques et migrations. R100 ne crée aucun second éditeur,
VM, stockage de sauvegarde ou accès agent : les cinq outils restent derrière le document
ForgeScript canonique, une configuration transactionnelle, le runtime natif borné et les surfaces
MCP existantes.

## Matrice d’écarts auditée

| Exigence source | Présent avant R100 | Limite réelle fermée par R100 | Preuve |
|---|---|---|---|
| TestWorld déterministe, fixtures, assertions, mocks et rapport CI | Exécution unitaire compacte et JSON sommaire | Pas de validation de suite, quota de cas, services mockés exposés ni preuve de déterminisme | `ForgeScriptQualityServiceTests`, adaptateur, Toolset et runtime |
| CPU, instructions, allocations, hotspots, graphe d’appels et alertes par handler | Somme globale de samples | Samples non agrégés par handler, pas de graphe structuré ni alertes localisées | tests de profilage, panneau et marqueurs R100 |
| Référence API, exemples, disponibilité kit/côté et ressource MCP depuis les métadonnées | Markdown minimal | Gouvernance générée et exposition de la référence/ressources dans `get_api` incomplètes | générateur, test MCP, `docs/generated/forgescript-api.md` |
| Lints heartbeat, requête unsafe, null/validité, API dépréciée et autorité | Cinq détections textuelles compactes | Pas de règles configurables/versionnées, chemin localisé ni politique warnings-as-errors | tests statiques et diagnostics cliquables visibles |
| Persistance des seuls composants enregistrés, sans pointeur VM, migration et validation | Filtrage par nom enregistré | Version du composant perdue, migration incorrecte, pas de validation de valeur ni quota d’enveloppe | tests d’état, adaptateur et smoke runtime |
| Configuration canonique et transactions partagées | Aucune configuration qualité persistée | Identités, schéma, provenance, API, event binding, preview/apply/reject, Undo/Redo et roundtrip absents | profil/validateur/history/store et tests |
| Extension runtime optionnelle/requise | Aucun adaptateur qualité | Aucun fallback optionnel, diagnostic requis ou rejet de handle obsolète | `ForgeScriptQualityRuntimeAdapterTests` |

## Acceptation partagée

- `ForgeScriptQualityProfile` porte UUID uniques, schéma v1, provenance, API, binding d’événement et
  réglages validés des cinq capacités.
- `ForgeScriptQualityHistory` est l’unique frontière de mutation avec preview, application atomique,
  rejet sans changement et Undo/Redo ; `ForgeScriptQualityStore` conserve exactement les valeurs.
- Une extension absente est ignorée si elle est optionnelle et produit
  `FORGESCRIPT_QUALITY_REQUIRED_MISSING` si elle est requise ; les handles obsolètes sont refusés.
- Le cas nominal compile et s’exécute dans la sandbox native. Un script abusif est arrêté par quota,
  puis une exécution saine réussit dans le même runtime.
- MCP conserve `get_api`, compile, diagnostics, run_tests et assign_event sans accès fichier,
  réseau, processus, objet moteur ou .NET arbitraire ; `get_api` inclut les cinq outils et la
  référence générée.

## Changements bornés livrés

- Configuration qualité canonique validée, historique transactionnel et stockage JSON atomique.
- TestWorld et suites déterministes avec mocks, assertions, quotas et rapport CI.
- Profilage agrégé par handler avec graphe d’appels et alertes de seuil localisées.
- Référence API gouvernée et générée depuis le catalogue versionné, avec exemples, disponibilité et
  ressources MCP.
- Analyse statique configurable à cinq familles avec chemins cliquables.
- Enveloppe d’état versionnée, composants enregistrés, validation, migration et quota sans état VM.
- Adaptateur d’application unique, cinq résumés Toolset et panneau runtime accessible.

## Preuves reproductibles

- Tests ciblés : 7/7 service qualité, 2/2 adaptateur runtime et 7/7 agent MCP.
- Marqueur Toolset : `OPENRPG_FORGE_TOOLSET_R100_SCRIPT_QUALITY_PASS`.
- Marqueur runtime : `OPENRPG_FORGE_RUNTIME_R100_SCRIPT_QUALITY_PASS`.
- Marqueur visuel : `OPENRPG_FORGE_R100_SCRIPT_QUALITY_UI_CAPTURE_PASS`.
- Preuve visuelle : `docs/aurora-parity/evidence/r100-script-quality-ui.png`, inspectée à
  1680×1000. Les cinq capacités, la console compilée et les diagnostics localisés sont lisibles sans
  chevauchement.
- Transcript du portail intégré : `.artifacts/r100-full-verify-final.log`, avec 1 708 tests .NET,
  39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`. Le portail valide les deux marqueurs moteur
  R100 exacts dans leurs journaux de scénario.

## Non-objectifs

- `FC-SCRIPT-P1-05` et les lots P1 suivants ;
- toutes les fonctions P2 et Asset/Map AV7–AV10 ;
- commit ou push.
