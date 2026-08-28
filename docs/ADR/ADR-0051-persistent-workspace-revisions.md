# ADR-0051 — Révisions persistantes et transactions de workspace

Date : 28 août 2026
Statut : accepté
UUID : `dcd1d984-4b61-4d32-9368-1dff8c8fb762`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Studio et le host MCP peuvent ouvrir simultanément le même workspace. Une révision maintenue
uniquement dans la mémoire de chaque processus ne permet pas de détecter qu'un autre processus a
modifié `project.json` ou un document de zone. Une sauvegarde pouvait donc remplacer silencieusement
le travail le plus récent. De plus, la sérialisation d'un projet réparti sur plusieurs fichiers
n'offrait pas de récupération globale après une interruption entre deux remplacements atomiques.

## Décision

- `Forge.ModuleFormat` persiste une révision opérationnelle et l'empreinte SHA-256 du manifeste, de
  l'index projet et de tous les documents de zone dans `.forge/project-session.json`.
- L'ouverture récupère d'abord toute transaction interrompue, puis charge la révision correspondant
  à l'empreinte réellement observée. Une modification externe reconnue avance la révision effective.
- Toute sauvegarde prend un verrou exclusif interprocessus, recalcule l'empreinte du disque et refuse
  la mutation avec `workspace.revision_conflict` si elle ne correspond plus à la session ouverte.
- Le projet, ses zones et l'état de révision sont préparés dans un journal durable sous
  `.forge/project-transactions`. Une transaction non marquée comme validée est annulée à la prochaine
  ouverture ; une transaction validée mais non nettoyée est simplement finalisée.
- Studio et MCP initialisent leur historique de commandes avec la révision persistée. Ils transmettent
  la révision produite par la commande à la sauvegarde et ne publient l'état en mémoire qu'après son
  succès.
- Les fichiers `.forge` concernés sont des données opérationnelles locales. Ils n'entrent ni dans le
  format canonique du module, ni dans un package `.orpmod`.

## Preuves

- Tests : `tests/Forge.ModuleFormat.Tests/Workspaces/ProjectSessionTests.cs`
- Tests : `tests/Forge.Mcp.Tests/Projects/ForgeProjectAgentServiceTests.cs`
- Suite : `tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj` (458 tests)
- Suite : `tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj` (180 tests)

## Conséquences

Deux processus ne peuvent plus écraser silencieusement leurs modifications. Une session obsolète doit
recharger le workspace et rejouer sa commande, ce qui conserve le modèle transactionnel existant sans
inventer une fusion automatique de documents RPG. Les sauvegardes multi-fichiers deviennent
récupérables après une interruption simulée ou réelle.

Le verrou protège les opérations locales sur un même système de fichiers ; cette décision ne fournit
ni édition collaborative réseau, ni résolution sémantique de conflits. Une copie de workspace qui
omet `.forge` repart volontairement à la révision opérationnelle zéro, tandis que son contenu canonique
reste inchangé.

## Remplacement

Cette décision ne remplace aucun ADR. Elle complète la gouvernance transactionnelle des mutations
Studio et MCP.
