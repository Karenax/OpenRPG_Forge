# G12-G — Création et réparation ForgeScript assistées

## Portée

- exigence : `ORF-AI-018` ;
- récupérer la version et l'allowlist de l'API ForgeScript avant génération ;
- générer ou réparer une source confinée à `scripts/*.forge` ;
- compiler, inspecter les diagnostics et exécuter les tests de sandbox avant toute écriture ;
- partager le même cycle plan/apply/revert entre Toolset et MCP.

## Résultat livré

Le service G12-G rend explicites UUID, schéma v1, binding d'événement, API ForgeScript 1.0.0, VM
AngelScript 2.38.0, source, hash avant/après, citations, style, coût, provenance et diff. La source
déterministe n'utilise que les signatures allowlistées. Une source corrigée par l'auteur dans
l'écran repasse obligatoirement par compilation native, contrôle de version API et exécution sous
quotas. Les erreurs structurées empêchent l'application.

Le handler confine le chemin au workspace, vérifie le hash du fichier courant, lie transaction,
plan et révision, puis écrit atomiquement uniquement après approbation et dry-run exact. Son patch
conserve la source antérieure ; annuler, rétablir et le revert MCP restaurent le fichier réel.

La fenêtre **ForgeScript assisté** affiche brief, binding, source éditable, API/VM, compilation,
tests, diagnostics, citations, coût, provenance, hashes et diff. Le MCP expose les quatre mêmes
étapes propose/plan/apply/revert, sans outil d'exécution système générique.

## Non-objectifs

- aucun accès fichier, réseau, processus, thread, DLL ou API moteur depuis le script ;
- aucun commit d'une source non compilée ou dont la sandbox échoue ;
- aucun fournisseur réseau obligatoire ;
- la création de boss assistée reste le périmètre de G12-H (`ORF-AI-019`).

## Preuves

- tests ModuleFormat : génération déterministe, API/VM, compilation, sandbox, chemin sûr,
  diagnostics, approbation, permission, dry-run exact et undo/redo ;
- test MCP : propose/plan/apply/revert avec source persistée, recompilée puis supprimée/restaurée ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
