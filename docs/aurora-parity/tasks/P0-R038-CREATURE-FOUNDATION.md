# P0 R038 — fondation de créature

Lot stable : `R038` / `FC-CREATURE-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-CRE-002` — Blueprint de créature.
- `ORF-CRE-003` — Identité et texte.
- `ORF-CRE-014` — Santé et ressources.
- `ORF-CRE-027` — Vitesse et profils de déplacement.
- `ORF-CRE-033` — OnSpawn.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R038 |
|---|---|---|
| composition créature | `CreatureDefinition` et factory RPG Kit isolées | profil canonique relié au blueprint et au projet, références de composants, transform et overrides |
| identité narrative | UUID, slug et nom dans le modèle RPG Kit | noms localisés, aliases, pronoms, biographies, notes et tags persistés et éditables |
| ressources | pools génériques du RPG Kit | ressources UUID bornées, régénération/persistance, validation projet et aperçu |
| déplacement | six modes et contraintes du RPG Kit | contrat persistant, formulaire, validation des valeurs et résolution runtime |
| apparition | valeur `CreatureSpawned` isolée | binding ForgeScript versionné, contexte/seed/autorité et émission seulement après validation complète |
| auteur/agents | aucun écran spécialisé connecté | formulaire à sections, aperçu, commande atomique, undo/redo, lecture typée et MCP filtré/dry-run |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| domaine canonique | `CreatureFoundationProfile`, `CreatureFoundationValidator`, `BlueprintDefinition.CreatureFoundation` | `CreatureFoundationProfileTests` |
| mutation atomique | `SetCreatureFoundationHandler` et historique projet | `SetCreatureFoundationHandlerTests`, smoke Toolset R038 |
| persistance exacte | sérialiseurs projet/workspace 0.6.34 / 1.28.0 | tests `CreatureFoundation_round_trips` et fixture Crypte |
| validation pré-build | `ProjectValidator` valide les profils et refuse les UUID dupliqués | `ProjectValidationRejectsInvalidAndDuplicateFoundationsBeforeBuild` |
| écran spécialisé | `CreatureFoundationWindow`, sections identité/ressources/déplacement/composants/apparition | capture inspectée `evidence/r038-creature-foundation-ui.png` et détails |
| runtime sûr | `CreatureFoundationRuntimeResolver`, configuration puis `Spawn` validé | marqueur `OPENRPG_FORGE_RUNTIME_R038_CREATURE_FOUNDATION_PASS` |
| ForgeScript et MCP | `CreatureFoundationScriptService`, ressource filtrée et plan dry-run | test MCP fondation et smoke Toolset R038 |

## Non-objectifs

- ajouter les événements dégâts/mort du lot R039 ;
- ouvrir une fonction P1/P2 ou AV7–AV10 ;
- muter directement un fichier, un nœud moteur ou le projet via MCP ;
- commit ou push Git.

## Validation finale

- tests ciblés Domain, Application, ModuleFormat et MCP verts ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_PASS` ;
- captures 1528×940 inspectées sous le backend de compatibilité ;
- smoke runtime et portail intégré exécutés avant promotion finale des cinq fonctions vers `Tested` ;
- tracker, backlog, documentation générée et graphe Roslyn régénérés avant ouverture de R039.
