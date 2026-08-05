# G11-Q — Manifestes, crash, santé, récupération et synchronisation

Date de fermeture : 5 août 2026.

## Résultat observable

Le Creator édite un profil opérationnel persistant et annulable. Il affiche la santé du projet,
autosauvegarde le travail et propose une récupération comparée lorsqu’un snapshot est plus récent.
Le CLI valide les tâches, produit le rapport de santé, inspecte la récupération, prépare une
synchronisation et capture un artefact de crash sanitizé. Runtime et MCP lisent les mêmes contrats.

## Contrats livrés

- `AgentTaskManifest` porte UUID, schéma, clé, objectif, scope, interdictions, acceptation,
  propriétaire, dépendances et provenance ; le validateur détecte chemins dangereux, dépendance
  absente et cycle ;
- `CrashArtifactBuilder` écrit atomiquement un ZIP borné avec manifeste, logs expurgés, version,
  SHA-256 du package, reproduction ID et dump facultatif via `ICrashDumpAdapter` ;
- `ProjectHealthReportService` agrège diagnostics, tests, performance, licences, migrations et
  couverture avec sévérité, statut global, empreinte déterministe et décision de build ;
- `RecoverySnapshotStore` journalise, inspecte, met en quarantaine et compare hashes et compteurs
  avant restauration ; l’ouverture du projet propose le dernier snapshot pertinent ;
- `ContentRepositorySyncService` planifie ou applique une reprise partielle, vérifie manifeste,
  signataire, SPDX et SHA-256, puis déduplique dans un cache adressé par contenu sous quota.

## Persistance et migrations

- projet agrégé : `0.6.19`, migration depuis `0.6.18` ;
- workspace scindé : `1.13.0`, migration depuis `1.12.0` ;
- trois workspaces du dépôt migrés avec UUID déterministes et chemins de stockage relatifs sûrs ;
- le Creator réutilise `SetRuntimeExtensions`, donc dry-run, diagnostics, undo/redo et round-trip.

## Preuves automatiques

- `OperationalReadinessProfileTests` : valeurs stables, cycles, dépendances et chemins ;
- `ProjectHealthReportServiceTests` : six domaines, ordre, empreinte et blocage ;
- `CrashArtifactBuilderTests` : sanitization, métadonnées et adaptateur de dump ;
- `RecoverySnapshotStoreTests` : journal, candidat, comparaison et quarantaine ;
- `ContentRepositorySynchronizationTests` : reprise, déduplication, confiance, licence et altération ;
- `ForgeProjectAgentServiceTests` : rapport MCP structuré et lecture seule ;
- markers `OPENRPG_FORGE_TOOLSET_G11Q_RESILIENCE_PASS`,
  `OPENRPG_FORGE_RUNTIME_G11Q_RESILIENCE_PASS` et `OPENRPG_FORGE_CRASH_ARTIFACT_PASS`.
- gate global `scripts/Verify.ps1` validé le 5 août 2026 : 671 tests .NET, 24 tests Python
  généraux, 11 tests Python du pipeline d’assets, graphe frais, Toolset/runtime réels, capture
  visuelle, worker Blender réel et marker final `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

Le lot fournit la frontière `ICrashDumpAdapter`, pas un collecteur natif universel. Le transport de
synchronisation reste injecté afin que HTTP, serveur Forge ou stockage local puissent partager les
mêmes règles ; les canaux de publication et le registre curaté sont traités par des lots ultérieurs.
