# P1 R120 — Tests assistés et cohérence narrative

Lot stable : `R120` / `FC-AI-P1-03` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AI-025` — agent de test ;
- `ORF-AI-026` — simulation de dialogue ;
- `ORF-AI-027` — simulation de quête ;
- `ORF-AI-029` — contrôle de cohérence narrative ;
- `ORF-AI-030` — prompts versionnés, déjà `Tested` et conservé sans promotion artificielle.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/24_AI.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R120 |
|---|---|---|
| identité et configuration | profil assistant UUID/schéma/provenance, persistance et historique | profil de test canonique, borné, optionnel/requis et limité à quatre actions gameplay |
| agent de test | runner de scénarios processus et services QA spécialisés | agent en processus, sans shell, rapportant blocages, couverture et reproduction structurée |
| dialogue | simulateur et lint canoniques | chemins bornés, contradictions, tonalité et issues manquantes réunis dans un rapport stable |
| quête | moteur de simulation et validation | états inaccessibles, boucles, récompenses exploitables, divergence multijoueur et sauvegarde/reprise |
| cohérence narrative | index et contrôles qualité déterministes | conflits cités avec sources et transaction suggérée séparée, sans réécriture silencieuse |
| surface humaine et agent | assistant transactionnel, UI et MCP existants | laboratoire visible, bouton de test, résultats/erreurs, MCP borné et préflight build |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| profil et allowlist | `AssistantTestAutomationProfile`, `AssistantWorkspaceProfileValidator` | tests Domain des bornes, actions et résolution optionnelle/requise |
| agent in-process | `AssistantScenarioTestService` | tests Application déterministes et test MCP sans shell |
| dialogue et quête | services de simulation existants plus diagnostic agrégé | fixtures positives/négatives avec contradictions et cycle de récompense |
| cohérence narrative | constats avec citations, reproduction et suggestion séparée | fixture de clés de localisation conflictuelles |
| transaction et persistance | `SetAssistantWorkspaceProfileHandler`, store JSON | dry-run, empreinte, undo/redo, round-trip et migration legacy |
| pré-build | `ModulePackager.Export` | profil invalide refusé avant création de `.orpmod` |
| interface | `AssistantWorkspaceWindow` | capture inspectée `evidence/r120-ai-test-lab-ui.png` |
| portail | marqueur Toolset R120 | `OPENRPG_FORGE_TOOLSET_R120_AI_TEST_AUTOMATION_PASS` |

## Non-objectifs

- simulation de combat P2 ou bot autonome généraliste ;
- exécution de shell, Python libre, réseau ou moteur externe par l’agent ;
- correction automatique de la narration sans plan et approbation ;
- lots AV7–AV10, commit ou push.

## Validation finale attendue

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1600×960 inspectée avec couverture, actions autorisées, empreinte et résultat ;
- tracker/backlog, documentation et graphe régénérés ;
- `scripts/Verify.ps1` terminé sur `OPENRPG_FORGE_VERIFICATION=PASS`.
