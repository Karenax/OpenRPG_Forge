# P0 R001 — profil et chaîne de build canoniques

Lot stable : `R001` / `FC-BUILD-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-BUILD-001` — Dépôt Git canonique.
- `ORF-BUILD-002` — Structure de projet standard.
- `ORF-BUILD-003` — Build CLI.
- `ORF-BUILD-004` — Build déterministe.
- `ORF-BUILD-005` — Build incrémental.
- Source immuable relue intégralement :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/25_BUILD.md`, sections 001 à 005.
- Dépendances déclarées réévaluées : `CONTENT`, `SCRIPT`, `QA`. Le lot réutilise le projet canonique,
  la compilation ForgeScript et le laboratoire qualité ; il n'introduit aucun cycle de domaine.

## Fichiers et composants

- Domaine : `WorkspaceBuildProfile`, son validateur, `BuildDependencyGraph`.
- Application : commande `SetWorkspaceBuildProfile`, dry-run, audit, révision et historique.
- Persistance : `build.profile.json`, écriture atomique et cache `.forge/build-graph.json`.
- Auteur : `BuildPipelineWindow`.
- Automatisation : `Forge.Cli`, `ForgeProjectAgentService`, `ForgeMcpTools`.
- Packaging : `ModulePackager`, plan incrémental et contrôle byte-identique.
- Preuves : tests Application, ModuleFormat, MCP et smoke test Toolset.

## Non-objectifs

- aucune fonction P1/P2 ;
- aucun lot AV7 à AV10 ;
- aucun travail de CI, merge assisté, packaging Windows ou release channel ;
- aucun transformeur 3D avancé ajouté au graphe ;
- aucun push Git.

## Risques contrôlés

- Une configuration invalide ne doit jamais remplacer la configuration valide : validation avant
  écriture et remplacement atomique.
- Un build ne doit pas modifier les sources : seuls la destination explicite et le cache jetable
  `.forge/` peuvent changer.
- Un cache corrompu doit rester supprimable : le plan retombe sur une reconstruction complète.
- Une archive inchangée ne doit pas être réémise ; un changement d'option de manifeste doit toutefois
  forcer la réécriture.
- Les commandes MCP sont typées et bornées et ne donnent aucun accès à un shell arbitraire.

## Matrice du contrat source commun

| Critère source | État après R001 | Preuve |
|---|---|---|
| Modèle canonique avant représentation moteur | présent | `WorkspaceBuildProfile`, `BuildDependencyGraph` dans `Forge.Domain` |
| Une seule voie de mutation UI/domaine | présent | `BuildPipelineWindow` appelle `SetWorkspaceBuildProfileHandler` |
| Extension optionnelle absente ignorée proprement | présent | `WorkspaceBuildProfileStore.Load` rend le profil sûr par défaut |
| Extension requise invalide diagnostiquée | présent | `WorkspaceBuildProfileValidator` et codes `build.profile.*` |
| Code stable, chemin et UUID si possible | présent | diagnostics localisés `build/...`, UUID du profil |
| UUID stable | présent | `WorkspaceBuildProfile.DefaultId` et round-trip JSON |
| `schemaVersion` | présent | schéma v1 validé strictement |
| Provenance de modification | présent | propriété canonique `Provenance` |
| Propriété visible dans un écran spécialisé | présent | carte « Profil canonique de build » du Build Pipeline |
| État vide et erreur explicites | présent | absence = défaut signalé ; erreur = code + chemin dans l'écran |
| Pas d'API gameplay obligatoire | présent | profil limité aux couches build/application/format |
| Build/validation accessibles au CLI | présent | commandes top-level structurées |
| MCP read/build/test/validate sans shell | présent | cinq outils typés du host MCP |
| Champs obligatoires | présent | validateur du profil |
| UUID uniques | présent | un UUID non vide pour le profil ; graphe refuse les doublons |
| Références résolues | présent | chemins uniques/sûrs et dépendances du graphe résolues |
| Version de schéma supportée | présent | rejet `build.profile.schema_unsupported` |
| Mutations de configuration transactionnelles | présent | dry-run, patch/inverse, révision, audit, sauvegarde atomique |
| Tests/builds en lecture seule sur les sources | présent | sorties explicites et cache `.forge/` uniquement |

## Matrice des adaptations obligatoires

| Fonction | Limite constatée avant le lot | Fermeture réelle |
|---|---|---|
| BUILD-001 | politique Git/textes/générés/assets seulement documentée | politique canonique validée, visible, versionnable et documentée ; `.forge/` explicitement ignoré |
| BUILD-002 | structure du workspace implicite et minimale | huit chemins standards canoniques, uniques, relatifs et configurables |
| BUILD-003 | seulement sous-commandes de packaging | `build`, `validate`, `test`, `pack`, `migrate`, `diff`, `references`, sorties structurées et codes 0/1/2 |
| BUILD-004 | pas de preuve complète de deux builds propres | test byte-identique, timestamps ZIP normalisés, ordre stable, hashes et contrôle CLI de reproductibilité |
| BUILD-005 | archive inchangée encore réémise | sortie existante réutilisée si graphe et manifeste sont identiques ; impact transitif conservé |

## Matrice des 25 critères d'acceptation

| Fonction | Configuration sans édition d'un généré | Save/close/reload exact | Invalide refusé avant build | UI = commande domaine | Undo/redo |
|---|---|---|---|---|---|
| BUILD-001 | écran et `build.profile.json` source | test store déterministe | `repository_policy_invalid` | handler unique | historique testé |
| BUILD-002 | huit champs de chemins | dictionnaire exact au reload | `layout_invalid` localisé | handler unique | historique testé |
| BUILD-003 | opérations immuables visibles dans le profil | liste exacte au reload | `cli_contract_invalid` | CLI et UI consomment le même profil | historique testé |
| BUILD-004 | cases de politiques déterministes | booléens exacts au reload | `determinism_invalid` | build et UI valident le même profil | historique testé |
| BUILD-005 | cases incrémental/impact et chemin cache | état exact au reload | `incremental_invalid` / `layout_invalid` | plan partagé UI/CLI/MCP | historique testé |

## Tests exacts du palier

1. `dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore --filter FullyQualifiedName~WorkspaceBuildProfileCommandTests`
2. `dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspaceBuildProfileStoreTests|FullyQualifiedName~WorkspaceBuildGraphTests"`
3. `dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore --filter FullyQualifiedName~BuildAgentServiceTests`
4. CLI isolé : `build-profile set-default`, `validate`, `diff`, `references`, `test`, `build`.
5. Toolset headless : marqueur `OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS ... profile=canonical ui=previewed`.
6. Graphe : `generate`, puis `check --full`.
7. Tracker et backlog : régénération puis `-Check`.
8. Portail global : `scripts/Verify.ps1`.

Le statut `Tested` n'est autorisé pour les cinq lignes qu'après réussite des huit paliers ci-dessus.

## Clôture vérifiée

- tests ciblés : 14 réussis sur les suites Domain, Application, ModuleFormat et MCP ;
- CLI isolé : `R001_CLI_SMOKE_PASS`, avec comparaison reproductible de deux builds propres ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS profile=canonical ui=previewed` ;
- preuve visuelle inspectée : `docs/aurora-parity/evidence/r001-build-profile-ui.png` ;
- tracker/backlog : 212 fonctions `Tested`, 186 P0 non closes et 49 lots P0 ouverts ;
- graphe : 5 198 nœuds, 30 248 relations, zéro diagnostic, contrôle complet déterministe ;
- portail post-régénération : 27 projets, 1 005 tests .NET, 6 tests Python généraux,
  33 tests Python asset et `OPENRPG_FORGE_VERIFICATION=PASS`.

Les cinq lignes `ORF-BUILD-001` à `ORF-BUILD-005` sont closes individuellement. Aucun P1/P2,
aucun lot AV7–AV10 et aucune publication distante n'ont été exécutés dans R001.
