# Build, collaboration, versionnement et automation

**Domaine : `BUILD`**  
**Préfixe : `ORF-BUILD`**  
**Couche dominante : Forge Core Tooling**  
**Nombre d’exigences : 25**  
**Sources principales : S02, S04, S13, S17, S18, S20**

## Finalité

Sources textuelles, Git, builds déterministes, import/export en masse, cache, migration, CI, branches et édition collaborative.

## Règles de domaine

- Le domaine respecte les décisions architecturales du MEGACONTEXT OpenRPG Forge.
- Une propriété historique liée à D&D est reformulée comme donnée de ruleset ou option de kit.
- Les noms historiques sont utilisés uniquement pour expliquer la référence.
- Les données canoniques sont textuelles, versionnées et indépendantes des ressources Godot.
- Les opérations d’édition passent par commandes, validation et undo/redo.
- Les opérations runtime sensibles sont autoritaires côté serveur lorsqu’un Network Kit est actif.
- Les opérations IA utilisent les mêmes commandes via MCP.

## Index des exigences

| ID | Fonctionnalité | Priorité | Origine |
|---|---|---:|---|
| [ORF-BUILD-001](#orf-build-001) | Dépôt Git canonique | P0 | Ecosystem modernization |
| [ORF-BUILD-002](#orf-build-002) | Structure de projet standard | P0 | Forge extension |
| [ORF-BUILD-003](#orf-build-003) | Build CLI | P0 | Ecosystem modernization |
| [ORF-BUILD-004](#orf-build-004) | Build déterministe | P0 | Forge extension |
| [ORF-BUILD-005](#orf-build-005) | Build incrémental | P0 | Enhanced Edition |
| [ORF-BUILD-006](#orf-build-006) | Cache jetable | P0 | Native Aurora modernized |
| [ORF-BUILD-007](#orf-build-007) | Import/export en masse | P1 | Official expansion |
| [ORF-BUILD-008](#orf-build-008) | Normalisation des documents | P0 | Forge extension |
| [ORF-BUILD-009](#orf-build-009) | Migrations | P0 | Forge extension |
| [ORF-BUILD-010](#orf-build-010) | Références inverses | P0 | Forge extension |
| [ORF-BUILD-011](#orf-build-011) | Diff sémantique | P0 | Forge extension |
| [ORF-BUILD-012](#orf-build-012) | Merge assisté | P1 | Ecosystem modernization |
| [ORF-BUILD-013](#orf-build-013) | Worktrees et agents | P0 | Forge extension |
| [ORF-BUILD-014](#orf-build-014) | CI | P0 | Forge extension |
| [ORF-BUILD-015](#orf-build-015) | Fixtures de référence | P0 | Forge extension |
| [ORF-BUILD-016](#orf-build-016) | Build multi-plateforme | P2 | Enhanced Edition inspired |
| [ORF-BUILD-017](#orf-build-017) | Packaging Windows | P1 | Forge extension |
| [ORF-BUILD-018](#orf-build-018) | Versioning sémantique | P0 | Forge extension |
| [ORF-BUILD-019](#orf-build-019) | ADRs | P0 | Forge extension |
| [ORF-BUILD-020](#orf-build-020) | Documentation générée | P1 | Ecosystem inspired |
| [ORF-BUILD-021](#orf-build-021) | Task manifests | P1 | Forge extension |
| [ORF-BUILD-022](#orf-build-022) | Dependency approval | P0 | Forge extension |
| [ORF-BUILD-023](#orf-build-023) | Third-party notices | P0 | Forge extension |
| [ORF-BUILD-024](#orf-build-024) | Release channels | P2 | Enhanced Edition inspired |
| [ORF-BUILD-025](#orf-build-025) | Crash artifacts | P1 | Enhanced Edition inspired |

## Exigences détaillées

<a id="orf-build-001"></a>

### ORF-BUILD-001 — Dépôt Git canonique

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | S17 |
| Tags | `git`, `source`, `collaboration` |

**Comportement Aurora de référence.** Aurora modules are not naturally merge-friendly; community tools extract sources.

**Adaptation obligatoire pour OpenRPG Forge.** Project is a normal Git repository with text documents, explicit generated files and reproducible assets.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Dépôt Git canonique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-002"></a>

### ORF-BUILD-002 — Structure de projet standard

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | Conception Forge |
| Tags | `layout`, `project`, `convention` |

**Comportement Aurora de référence.** Historical projects mix module/resources in containers and folders.

**Adaptation obligatoire pour OpenRPG Forge.** Stable directory layout for data, scripts, assets, localization, tests, packages, docs and generated cache.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Structure de projet standard » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-003"></a>

### ORF-BUILD-003 — Build CLI

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | SCRIPT, CONTENT |
| Sources | S17, S18 |
| Tags | `cli`, `build`, `automation` |

**Comportement Aurora de référence.** Aurora builds from Toolset; community adds command-line tooling.

**Adaptation obligatoire pour OpenRPG Forge.** `forge build`, `validate`, `test`, `pack`, `migrate`, `diff`, `references` with stable exit codes.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Build CLI » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-004"></a>

### ORF-BUILD-004 — Build déterministe

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `deterministic`, `build`, `hash` |

**Comportement Aurora de référence.** Binary module builds can vary and are hard to review.

**Adaptation obligatoire pour OpenRPG Forge.** Stable ordering, normalized timestamps, locked dependencies, content hashes and reproducibility check.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Build déterministe » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-005"></a>

### ORF-BUILD-005 — Build incrémental

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | SCRIPT |
| Sources | S04 |
| Tags | `incremental`, `cache`, `build` |

**Comportement Aurora de référence.** EE optimizes build/update.

**Adaptation obligatoire pour OpenRPG Forge.** Dependency graph rebuilds only affected docs/scripts/assets while preserving correctness.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Build incrémental » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-006"></a>

### ORF-BUILD-006 — Cache jetable

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | S03 |
| Tags | `cache`, `generated`, `clean` |

**Comportement Aurora de référence.** Aurora caches compiled scripts.

**Adaptation obligatoire pour OpenRPG Forge.** All caches under explicit directory, content-addressed, invalidatable and never canonical.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Cache jetable » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-007"></a>

### ORF-BUILD-007 — Import/export en masse

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Official expansion |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | S02 |
| Tags | `batch`, `import`, `export` |

**Comportement Aurora de référence.** SoU adds bulk ERF import/export.

**Adaptation obligatoire pour OpenRPG Forge.** Batch commands with glob/type/filter, dry-run, conflict strategy, provenance and summary.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Import/export en masse » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-008"></a>

### ORF-BUILD-008 — Normalisation des documents

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `normalize`, `json`, `diff` |

**Comportement Aurora de référence.** Source text collaboration needs stable formatting.

**Adaptation obligatoire pour OpenRPG Forge.** Canonical serializer, line endings, property order, numeric formatting and `forge normalize`.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Normalisation des documents » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-009"></a>

### ORF-BUILD-009 — Migrations

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | Conception Forge |
| Tags | `migration`, `schema`, `version` |

**Comportement Aurora de référence.** Content formats evolve across patches.

**Adaptation obligatoire pour OpenRPG Forge.** Versioned migrations, dry-run report, backups, idempotence, fixtures and downgrade when feasible.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Migrations » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-010"></a>

### ORF-BUILD-010 — Références inverses

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | SHELL, AI |
| Sources | Conception Forge |
| Tags | `references`, `index`, `impact` |

**Comportement Aurora de référence.** Tags/resrefs make impact analysis difficult.

**Adaptation obligatoire pour OpenRPG Forge.** Indexed graph, `forge references`, editor and MCP access, cache rebuildable from source.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Références inverses » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-011"></a>

### ORF-BUILD-011 — Diff sémantique

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `diff`, `semantic`, `review` |

**Comportement Aurora de référence.** Binary/text diffs do not explain scene graph changes.

**Adaptation obligatoire pour OpenRPG Forge.** Domain diff for areas, graphs, entities, packages and scripts; output text/JSON.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Diff sémantique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-012"></a>

### ORF-BUILD-012 — Merge assisté

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | DIALOGUE, AREA |
| Sources | S17 |
| Tags | `merge`, `collaboration`, `uuid` |

**Comportement Aurora de référence.** Multiple builders need collaboration.

**Adaptation obligatoire pour OpenRPG Forge.** Three-way merge for UUID-based lists/graphs, conflict UI, preserve both variants and tests.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Merge assisté » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-013"></a>

### ORF-BUILD-013 — Worktrees et agents

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `agent`, `worktree`, `workflow` |

**Comportement Aurora de référence.** Codex/Hermes must not edit same worktree.

**Adaptation obligatoire pour OpenRPG Forge.** Documented branch/worktree workflow, task ownership, file scopes and integration checks.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Worktrees et agents » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-014"></a>

### ORF-BUILD-014 — CI

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `ci`, `test`, `build` |

**Comportement Aurora de référence.** Toolset builds are traditionally local.

**Adaptation obligatoire pour OpenRPG Forge.** CI validates schemas, builds C#/C++/scripts, runs tests, packs sample and verifies licenses.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « CI » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-015"></a>

### ORF-BUILD-015 — Fixtures de référence

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `fixture`, `test`, `module` |

**Comportement Aurora de référence.** Regressions need representative modules.

**Adaptation obligatoire pour OpenRPG Forge.** Tiny, vertical slice, stress, malicious and migration fixtures versioned in repository.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Fixtures de référence » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-016"></a>

### ORF-BUILD-016 — Build multi-plateforme

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core Tooling |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | S21 |
| Tags | `platform`, `build`, `profile` |

**Comportement Aurora de référence.** NWN EE supports multiple platforms; Forge starts Windows.

**Adaptation obligatoire pour OpenRPG Forge.** Build profiles separate editor/client/server/platform assets, with capability matrix and CI expansion.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Build multi-plateforme » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-017"></a>

### ORF-BUILD-017 — Packaging Windows

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `windows`, `package`, `installer` |

**Comportement Aurora de référence.** Initial user environment is Windows.

**Adaptation obligatoire pour OpenRPG Forge.** Reproducible installer/portable package, signed optional, prerequisites detection, logs and uninstaller.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Packaging Windows » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-018"></a>

### ORF-BUILD-018 — Versioning sémantique

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | Conception Forge |
| Tags | `semver`, `version`, `compatibility` |

**Comportement Aurora de référence.** Historical patches use build numbers.

**Adaptation obligatoire pour OpenRPG Forge.** SemVer for Core/Kits/packages, schema versions separate, compatibility rules and changelogs.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Versioning sémantique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-019"></a>

### ORF-BUILD-019 — ADRs

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Governance |
| Origine | Forge extension |
| Composants probables | `Forge.Cli`, `Forge.Tooling` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `adr`, `decision`, `documentation` |

**Comportement Aurora de référence.** Architecture decisions must survive agent sessions.

**Adaptation obligatoire pour OpenRPG Forge.** ADR template, statuses, links to tests/benchmarks and update rules.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « ADRs » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-020"></a>

### ORF-BUILD-020 — Documentation générée

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | SCRIPT |
| Sources | S15 |
| Tags | `docs`, `generation`, `api` |

**Comportement Aurora de référence.** Manuals and Lexicon are external references.

**Adaptation obligatoire pour OpenRPG Forge.** Generate feature catalog, script API, schemas, CLI help, package docs and coverage from source metadata.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Documentation générée » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-021"></a>

### ORF-BUILD-021 — Task manifests

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `task`, `agent`, `scope` |

**Comportement Aurora de référence.** Agents need bounded work.

**Adaptation obligatoire pour OpenRPG Forge.** Machine-readable task with goal, scope, forbidden changes, acceptance tests, owner and dependencies.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Task manifests » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-022"></a>

### ORF-BUILD-022 — Dependency approval

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Governance |
| Origine | Forge extension |
| Composants probables | `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `dependency`, `license`, `risk` |

**Comportement Aurora de référence.** Add-ons can become abandoned or incompatible.

**Adaptation obligatoire pour OpenRPG Forge.** Registry of dependencies, version, license, owner, rationale, update cadence and removal plan.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Dependency approval » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-023"></a>

### ORF-BUILD-023 — Third-party notices

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Governance |
| Origine | Forge extension |
| Composants probables | `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT, QA |
| Sources | Conception Forge |
| Tags | `license`, `notice`, `automation` |

**Comportement Aurora de référence.** Custom content has many authors/licenses.

**Adaptation obligatoire pour OpenRPG Forge.** Automated SPDX/NOTICE aggregation and build failure for unknown required licenses.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Third-party notices » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-024"></a>

### ORF-BUILD-024 — Release channels

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core Tooling |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | CONTENT |
| Sources | S02, S04, S13, S17, S18, S20 |
| Tags | `release`, `channel`, `rollback` |

**Comportement Aurora de référence.** EE has stable/development patch lines.

**Adaptation obligatoire pour OpenRPG Forge.** Dev, preview, stable channels with migration policy, rollback and compatibility tests.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Release channels » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-build-025"></a>

### ORF-BUILD-025 — Crash artifacts

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | S13 |
| Tags | `crash`, `diagnostic`, `privacy` |

**Comportement Aurora de référence.** Modern patching benefits from logs/profiling.

**Adaptation obligatoire pour OpenRPG Forge.** Capture sanitized logs, minidump adapter, version, package fingerprint and reproduction ID.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Outils read/build/test/validate à sortie structurée et sans shell arbitraire.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Crash artifacts » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
