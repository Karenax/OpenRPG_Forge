# P0 R002 — gouvernance reproductible du build

Lot stable : `R002` / `FC-BUILD-P0-03` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-BUILD-013` — Worktrees et agents.
- `ORF-BUILD-014` — CI.
- `ORF-BUILD-015` — Fixtures de référence.
- `ORF-BUILD-018` — Versioning sémantique.
- `ORF-BUILD-019` — ADRs.
- Source immuable relue intégralement :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/25_BUILD.md`, sections 013, 014,
  015, 018 et 019.
- Dépendances réévaluées : `AI`, `CONTENT`, `QA`. Le lot conserve les outils agent sans shell,
  s'appuie sur les modules de démonstration existants et réutilise le portail qualité R001.

## Audit avant implémentation

| Fonction | Présent | Limite réelle à fermer |
|---|---|---|
| BUILD-013 | discipline branche/worktree dans `AGENTS.md` | aucun modèle canonique, ownership, périmètre de fichiers, persistance, UI ou preuve d'intégration |
| BUILD-014 | workflow C# restore/format/build/test et `Verify.ps1` local | schémas, natif, ForgeScript, package exemple et licences absents du workflow CI ; configuration non canonique |
| BUILD-015 | Crypte, forêt, migrations et cas hostile exercés dans des tests dispersés | aucun catalogue versionné couvrant explicitement tiny, vertical, stress, malicious et migration |
| BUILD-018 | versions de formats et SemVer de manifeste validés localement | aucune politique Core/Kits/packages, compatibilité globale, séparation schémas ni changelog canonique |
| BUILD-019 | ADRs 0001 à 0041 présents | aucun template gouverné, statuts autorisés, preuve tests/benchmarks ou règle de remplacement contrôlée |

## Contrat minimal de fermeture

- étendre le profil canonique R001 avec une gouvernance v1 à UUID/provenance ;
- valider séparément worktrees, CI, fixtures, SemVer/compatibilité et ADR avec codes/path stables ;
- conserver une seule commande transactionnelle pour UI, CLI et MCP, avec dry-run et undo/redo ;
- persister atomiquement dans `build.profile.json` et charger un défaut sûr si le bloc optionnel manque ;
- fournir un validateur de preuves du dépôt sans mutation des sources ;
- rendre les cinq familles visibles et configurables dans l'écran Build Pipeline ;
- faire exécuter au workflow CI les schémas, C#, C++/ForgeScript, tests, package exemple et licences ;
- versionner le catalogue de cinq fixtures et la politique SemVer/ADR.

## Non-objectifs

- aucune fonction P1/P2 ;
- aucun lot AV7–AV10 ;
- aucun orchestrateur d'agents autonome ni shell générique dans MCP ;
- aucune publication, exécution distante ou modification du snapshot Aurora.

## Preuves attendues

1. tests Domain du profil et des diagnostics de gouvernance ;
2. tests Application de dry-run, application, undo/redo ;
3. tests ModuleFormat de round-trip atomique et audit du dépôt/fixtures/CI/ADR ;
4. test MCP de ressource structurée et validation sans shell ;
5. smoke Toolset et capture inspectée de l'onglet Gouvernance ;
6. génération et contrôle tracker/backlog/docs/graphe ;
7. `scripts/Verify.ps1` vert avant ouverture de R003.

## Matrice du contrat source commun

| Critère source | État R002 | Preuve |
|---|---|---|
| modèle canonique avant moteur | présent | `WorkspaceBuildGovernanceProfile` dans `Forge.Domain` |
| une seule mutation UI/domaine | présent | extension de `WorkspaceBuildProfile` et `SetWorkspaceBuildProfileHandler` |
| extension optionnelle absente | présent | ancien JSON sans `governance` recharge le défaut sûr v1 |
| extension requise invalide | présent | sept diagnostics `build.governance.*` localisés |
| UUID, schéma, provenance | présent | UUID profil/fixtures, schéma v1 et provenance persistés |
| propriété visible, vide/erreur | présent | onglet Gouvernance, résumé, preview et diagnostics |
| CLI/MCP sans shell | présent | `build-profile show/set-default/validate`, ressource MCP structurée |
| champs, unicité, références, schéma | présent | validateurs Domain et audit ModuleFormat du dépôt |
| transaction et concurrence | présent | dry-run, révision, patch/inverse, ownership exclusif |
| tests/builds non mutants | présent | audit en lecture seule, sorties limitées à destination et `.forge/` |

## Matrice des 25 critères d'acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide refusé | UI = commande | Undo/redo |
|---|---|---|---|---|---|
| BUILD-013 | préfixe, ownership et scopes dans l'onglet | profil JSON exact | `worktree_invalid` | handler partagé | historique testé |
| BUILD-014 | workflow et sept contrôles CI | profil JSON exact | `ci_invalid` | handler partagé | historique testé |
| BUILD-015 | catalogue et cinq catégories visibles | fixtures/UUID exacts | `fixtures_invalid` | handler partagé | historique testé |
| BUILD-018 | Core/Kits/package/changelog visibles | versions exactes | `versioning_invalid` | handler partagé | historique testé |
| BUILD-019 | dossier/template et preuves configurables | statuts/politiques exacts | `adr_invalid` | handler partagé | historique testé |

## Résultats de clôture

- 13 tests ciblés réussis : Domain 3, Application 3, ModuleFormat 6, MCP 1 ;
- build natif : `OPENRPG_FORGE_FORGESCRIPT_NATIVE_BUILD_PASS vm=2.38.0` ;
- CLI isolé : `OPENRPG_FORGE_R002_CLI_PASS`, profil cinq familles, package valide et licences vérifiées ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS ... governance=p0-r002 ...` ;
- preuve visuelle inspectée : `docs/aurora-parity/evidence/r002-build-governance-ui.png` (1440 × 980) ;
- CI versionnée : schémas, C#, C++/ForgeScript, tests, fixture audit, package exemple et licences.
- promotion strictement limitée aux cinq IDs du lot : tracker à 217 `Tested`, 423 `Implemented`,
  77 `Researched`, 181 fonctions P0 et 48 lots P0 encore ouverts ;
- autorités fraîches : documentation déterministe, backlog, tracker et graphe de 5 204 nœuds,
  30 249 relations et zéro diagnostic ;
- portail post-promotion : 27 projets compilés sans avertissement, 1 010 tests .NET, 6 tests Python
  généraux, 33 tests Python assets, smokes Toolset/runtime/3D/packages/MCP et
  `OPENRPG_FORGE_VERIFICATION=PASS`.

Le lot `R002` est clos. Le prochain lot autorisé est `R003` / `FC-QA-P0-03`.
