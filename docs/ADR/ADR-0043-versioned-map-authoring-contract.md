# ADR-0043 — Contrat versionné de création de maps par l'IA

Date : 2026-08-13
Statut : accepté
UUID : `a7430000-0000-4000-8000-000000000043`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le planificateur de zone appelait un fournisseur avec un prompt minimal, puis construisait toujours
la topologie avec le générateur C# déterministe sans interpréter la réponse. Un modèle distant ou
local ne pouvait donc pas réellement proposer la map. Copier les règles de génération dans plusieurs
prompts créerait en outre des variantes impossibles à auditer.

## Décision

- `docs/ai/MAP_AUTHORING_CONTRACT.md` devient le contrat statique versionné de planification de maps.
- `Forge.Application` embarque ce document comme ressource afin que Creator et MCP utilisent les
  mêmes octets, y compris dans une distribution sans dépôt source.
- Le contenu normalisé, sa version et son SHA-256 sont injectés dans le message système et dans la
  provenance du plan.
- Le brief, la zone cible, les kits disponibles, le contexte borné d'entités/navigation/terrain et le
  lore autorisé forment un paquet JSON dynamique distinct du contrat statique.
- Les fournisseurs `OpenAiCompatible` et `Ollama` doivent retourner la topologie JSON du schéma
  versionné. Leur réponse est convertie en UUID stables puis validée par les contrats de domaine.
- Une réponse invalide échoue sans mutation et sans remplacement silencieux par une autre topologie.
- `DeterministicOffline` reste un mode explicitement déterministe et conserve le générateur local ;
  il suit ensuite les mêmes validations, permissions, dry-run, diff, approbation et revert.

## Preuves

- Tests : `tests/Forge.Application.Tests/Assistant/AssistedAreaPlanWorkflowTests.cs`
- Contrat : `docs/ai/MAP_AUTHORING_CONTRACT.md`
- Validation intégrée : `scripts/Verify.ps1`

## Conséquences

Une proposition de modèle peut désormais influencer réellement la topologie sans acquérir de voie
d'écriture directe. Toute évolution incompatible du contrat ou du schéma de réponse exige une
nouvelle version de prompt et une mise à jour des tests. Le document ajoute des tokens fixes à chaque
requête de map ; sa taille doit donc rester bornée et son contenu ne doit pas dupliquer le contexte
narratif global ou les données changeantes du module.

## Remplacement

Cette décision ne remplace aucun ADR. Elle complète l'ADR 0012 sur le MCP transactionnel.
