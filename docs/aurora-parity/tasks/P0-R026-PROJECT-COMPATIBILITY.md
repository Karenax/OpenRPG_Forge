# P0 R026 — compatibilité et migrations

Lot stable : `R026` / `FC-PROJECT-P0-03` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PROJ-025` — Compatibilité et migrations.
- Source immuable relue intégralement : section correspondante de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/02_PROJECT.md`.
- Dépendances BUILD/CONTENT réévaluées : R001/R002/R016/R023 fournissent déjà build déterministe,
  SemVer, packages versionnés, migration dry-run, sauvegarde et idempotence.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Version minimale runtime et version de schéma | Le manifeste `.orpmod` porte `RuntimeMinimumVersion`; la gouvernance conserve les versions Core/packages/schémas | L’export utilise encore `0.1.0` par défaut et aucune politique canonique ne relie minimum runtime, format projet et caractère requis | Profil UUID v1 validé, valeur persistée et injectée automatiquement au manifeste |
| Migrations automatiques, dry-run et sauvegarde | `WorkspaceBuildCollaborationService` produit aperçu sans écriture, liste des documents, sauvegarde confinée, rollback, idempotence et refus explicite du downgrade | La configuration est répartie entre gouvernance/collaboration et n’expose pas une synthèse dédiée à l’auteur | Profil de compatibilité lié au contrat de migration existant, validation avant build et rapport visible |
| Domaine, UI et commande unique | `WorkspaceBuildProfile`, `SetWorkspaceBuildProfileHandler`, historique et `BuildPipelineWindow` sont canoniques et transactionnels | L’UI ne permet pas de choisir le minimum runtime, le format projet, automatique/requis | Champs dédiés dans l’onglet collaboration, même commande dry-run/apply, undo/redo |
| Persistance et legacy | `WorkspaceBuildProfileStore` est atomique, round-trip et fournit déjà des defaults aux sections absentes | Pas de section compatibility ni test legacy correspondant | Default optionnel sûr, round-trip exact, invalidité refusée sans écriture |
| Runtime optionnel/requis | Le loader valide le manifeste et le runtime sait charger workspace/package | La version minimale du manifeste n’est pas comparée à la version disponible | Contrat de compatibilité typé, optionnel absent sûr, requis incompatible diagnostiqué avant extraction/runtime |
| ForgeScript et MCP | Migration preview MCP structurée ; build profile lisible ; aucun shell arbitraire | Pas de handle de lecture de compatibilité ni mutation MCP dry-run/apply/revert du profil | Lecture filtrée, plan/diff sémantique et réversion, service ForgeScript de lecture sûr |
| Diagnostics et preuve visible | Codes migration stables, UI build et capture R023 existent | Aucune preuve R026 réunissant identité, versions, migration et compatibilité runtime | Tests Domain/Application/ModuleFormat/MCP, capture inspectée, marqueurs Toolset/runtime R026 |

## Contrat minimal de fermeture

- ajouter au profil de build une politique `ProjectCompatibilityProfile` UUID v1 avec provenance,
  minimum runtime, format projet, migrations automatiques et caractère requis au runtime ;
- valider les SemVer, identité, schéma et migration automatique avec codes/chemins stables ;
- préserver les profils legacy sans section par un default optionnel sûr ;
- faire consommer le minimum runtime par l’export et le vérifier avant extraction d’un package ;
- étendre l’UI/commande existante, fournir lecture ForgeScript et mutation MCP dry-run/apply/revert ;
- réutiliser le service R023 de migration au lieu de créer une seconde chaîne de migration.

## Fichiers probables

- `src/Forge.Domain/Build/ProjectCompatibilityProfile.cs` et `WorkspaceBuildProfile.cs` ;
- `src/Forge.Application/Build/` pour lecture typée ;
- `src/Forge.ModuleFormat/Build/`, `src/Forge.ModuleFormat/Packages/` ;
- `src/Forge.Mcp/Build/`, `src/Forge.Mcp/Projects/ForgeProjectAgentService.cs`, hôte MCP ;
- `apps/Forge.Toolset/addons/openrpg_forge/BuildPipelineWindow.cs` et `ForgeEditorPlugin.cs` ;
- `apps/Forge.Runtime/Runtime/GameRoot.cs`, tests spécialisés et `scripts/Verify.ps1`.

## Risques et tests exacts

- UUID vide, schéma futur, provenance absente, SemVer invalide et migrations automatiques désactivées ;
- fichier legacy sans section, round-trip exact et second save identique ;
- preview sans écriture, apply avec backup, second passage vide, limite de sauvegardes, downgrade refusé ;
- manifeste exporté avec le minimum configuré, runtime compatible accepté, requis trop récent refusé avec code/chemin ;
- UI et commande produisent le même profil, preview/apply/undo/redo atomiques ;
- ForgeScript ne retourne qu’un handle typé, MCP lecture filtrée et plan/diff/apply/revert sans shell ;
- capture sans débordement, smokes Toolset/runtime et portail global.

## Non-objectifs

- migration de contenu propriétaire, downgrade générique non supporté, stockage cloud de sauvegardes,
  P1/P2, AV7–AV10, commit ou push Git.

## Clôture vérifiée

- Le profil `ProjectCompatibilityProfile` porte UUID, schéma v1, provenance, minimum runtime,
  format projet, migration automatique et caractère requis ; les diagnostics SemVer et chemins sont
  stables et le profil legacy sans section reprend un défaut sûr.
- `ModulePackager` injecte le minimum configuré au manifeste. `ModulePackageLoader` refuse un module
  requis trop récent avant de créer le cache ; le même écart reste non bloquant si le contrat est
  optionnel.
- L’onglet « Collaboration & compatibilité » passe par la commande canonique et son historique.
  La capture inspectée `docs/aurora-parity/evidence/r026-project-compatibility-ui.png` (1440×980)
  montre les champs et garanties sans débordement.
- ForgeScript expose un handle de lecture typé. MCP fournit lecture filtrée puis preview/diff,
  apply atomique et revert sans shell arbitraire.
- Tests ciblés : Domain 5, Application 5, ModuleFormat 27 et MCP 2, tous verts. Build solution :
  27 projets, zéro avertissement et zéro erreur. Les smokes réels ont émis
  `OPENRPG_FORGE_TOOLSET_R026_PROJECT_COMPATIBILITY_PASS` et
  `OPENRPG_FORGE_RUNTIME_R026_PROJECT_COMPATIBILITY_PASS`.
- Promotion individuelle : `ORF-PROJ-025` passe de `Implemented` à `Tested`; aucune autre ligne ne
  change dans ce lot. Le portail global est consigné après régénération ci-dessous avant R027.
- Portail final `scripts/Verify.ps1` : PASS sur 27 projets (0 avertissement, 0 erreur), 1 224 tests
  .NET, 6 tests Python généraux, 33 tests Python assets, tracker/backlog/docs frais, graphe
  5 872 nœuds / 35 450 relations / 0 diagnostic, smokes Toolset/runtime, exports, réseau, MCP et
  `OPENRPG_FORGE_VERIFICATION=PASS`.
