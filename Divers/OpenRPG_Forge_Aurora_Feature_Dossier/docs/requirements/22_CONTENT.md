# Contenu personnalisé, paquets, localisation et pipeline d’assets

**Domaine : `CONTENT`**  
**Préfixe : `ORF-CONT`**  
**Couche dominante : Forge Core**  
**Nombre d’exigences : 31**  
**Sources principales : S02, S03, S08, S09, S11, S14, S17, S18, S19, S20, S22**

## Finalité

Packages versionnés, dépendances, priorité, import/export, synchronisation, localisation, modèles 3D, provenance et compatibilité.

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
| [ORF-CONT-001](#orf-cont-001) | Package de contenu versionné | P0 | Native Aurora modernized |
| [ORF-CONT-002](#orf-cont-002) | Ordre de priorité | P0 | Native Aurora |
| [ORF-CONT-003](#orf-cont-003) | Dépendances | P0 | Native Aurora modernized |
| [ORF-CONT-004](#orf-cont-004) | Import ERF en masse | P1 | Official expansion |
| [ORF-CONT-005](#orf-cont-005) | Export de blueprints | P1 | Native Aurora |
| [ORF-CONT-006](#orf-cont-006) | Sources textuelles | P0 | Ecosystem modernization |
| [ORF-CONT-007](#orf-cont-007) | Compilation de package | P0 | Native Aurora modernized |
| [ORF-CONT-008](#orf-cont-008) | Manifeste d’assets | P1 | Enhanced Edition |
| [ORF-CONT-009](#orf-cont-009) | Synchronisation automatique | P2 | Enhanced Edition |
| [ORF-CONT-010](#orf-cont-010) | Packs curatés | P3 | Enhanced Edition |
| [ORF-CONT-011](#orf-cont-011) | Localisation | P0 | Native Aurora modernized |
| [ORF-CONT-012](#orf-cont-012) | Police TrueType et UI scalable | P1 | Enhanced Edition |
| [ORF-CONT-013](#orf-cont-013) | Textures | P0 | Native Aurora modernized |
| [ORF-CONT-014](#orf-cont-014) | Modèles 3D | P0 | Ecosystem modernization |
| [ORF-CONT-015](#orf-cont-015) | Walkmeshes et collisions | P1 | Native Aurora modernized |
| [ORF-CONT-016](#orf-cont-016) | Animations | P1 | Native Aurora modernized |
| [ORF-CONT-017](#orf-cont-017) | VFX et emitters | P2 | Native Aurora modernized |
| [ORF-CONT-018](#orf-cont-018) | 2DA-style data tables | P1 | Native Aurora modernized |
| [ORF-CONT-019](#orf-cont-019) | Resource namespaces | P0 | Forge extension |
| [ORF-CONT-020](#orf-cont-020) | Content browser | P0 | Native Aurora modernized |
| [ORF-CONT-021](#orf-cont-021) | External edit round-trip | P1 | Ecosystem pattern |
| [ORF-CONT-022](#orf-cont-022) | Blender bridge | P1 | Ecosystem modernization |
| [ORF-CONT-023](#orf-cont-023) | Provenance d’asset | P0 | Forge extension |
| [ORF-CONT-024](#orf-cont-024) | Quarantaine d’import | P0 | Forge extension |
| [ORF-CONT-025](#orf-cont-025) | Compatibilité de plateforme | P1 | Forge extension |
| [ORF-CONT-026](#orf-cont-026) | Migrations de package | P1 | Forge extension |
| [ORF-CONT-027](#orf-cont-027) | Désinstallation sûre | P1 | Forge extension |
| [ORF-CONT-028](#orf-cont-028) | Signatures | P2 | Forge extension |
| [ORF-CONT-029](#orf-cont-029) | Delta updates | P2 | Enhanced Edition inspired |
| [ORF-CONT-030](#orf-cont-030) | Credits and notices generation | P0 | Forge extension |
| [ORF-CONT-031](#orf-cont-031) | IP boundary enforcement | P0 | Legal boundary |

## Exigences détaillées

<a id="orf-cont-001"></a>

### ORF-CONT-001 — Package de contenu versionné

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S02, S03 |
| Tags | `package`, `version`, `manifest` |

**Comportement Aurora de référence.** Aurora attaches HAK packs and imports ERF resources.

**Adaptation obligatoire pour OpenRPG Forge.** ForgePackage with manifest, semantic version, namespace, dependencies, assets, scripts, licenses and hashes.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Package de contenu versionné » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-002"></a>

### ORF-CONT-002 — Ordre de priorité

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `priority`, `override`, `conflict` |

**Comportement Aurora de référence.** HAK order controls resource override.

**Adaptation obligatoire pour OpenRPG Forge.** Explicit override graph, conflict rules, sealed assets, preview and deterministic resolution.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Ordre de priorité » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-003"></a>

### ORF-CONT-003 — Dépendances

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `dependency`, `lockfile`, `package` |

**Comportement Aurora de référence.** Modules list custom content packs manually.

**Adaptation obligatoire pour OpenRPG Forge.** Semantic dependency constraints, optional/peer dependencies, lockfile and offline resolution.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Dépendances » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-004"></a>

### ORF-CONT-004 — Import ERF en masse

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Official expansion |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | BUILD |
| Sources | S02 |
| Tags | `import`, `export`, `batch` |

**Comportement Aurora de référence.** SoU adds bulk import/export of resources.

**Adaptation obligatoire pour OpenRPG Forge.** Batch import/export with type filters, collision policy, provenance, dry-run and report.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Import ERF en masse » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-005"></a>

### ORF-CONT-005 — Export de blueprints

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | PALETTE |
| Sources | S02, S03 |
| Tags | `export`, `blueprint`, `dependencies` |

**Comportement Aurora de référence.** Aurora exports custom resources for reuse.

**Adaptation obligatoire pour OpenRPG Forge.** Export selected definitions plus transitive dependencies, licenses, migrations and sample previews.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Export de blueprints » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-006"></a>

### ORF-CONT-006 — Sources textuelles

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S17 |
| Tags | `source`, `text`, `git` |

**Comportement Aurora de référence.** Aurora modules are binary containers; tools like Nasher extract text sources.

**Adaptation obligatoire pour OpenRPG Forge.** Canonical JSON/text/YAML only where appropriate, one logical resource per file and Git-friendly layout.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sources textuelles » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-007"></a>

### ORF-CONT-007 — Compilation de package

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | SCRIPT |
| Sources | S01, S03 |
| Tags | `build`, `pack`, `cli` |

**Comportement Aurora de référence.** Toolset builds module and compiled scripts.

**Adaptation obligatoire pour OpenRPG Forge.** `forge pack` validates, compiles, imports, hashes, compresses and emits reproducible `.orpmod`.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Compilation de package » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-008"></a>

### ORF-CONT-008 — Manifeste d’assets

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | MULTI |
| Sources | S14 |
| Tags | `manifest`, `hash`, `asset` |

**Comportement Aurora de référence.** NWSync uses manifests and content repositories.

**Adaptation obligatoire pour OpenRPG Forge.** Content-addressed asset manifest with chunk hashes, compression, platform variants and provenance.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Manifeste d’assets » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-009"></a>

### ORF-CONT-009 — Synchronisation automatique

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | MULTI |
| Sources | S09, S14 |
| Tags | `sync`, `download`, `cache` |

**Comportement Aurora de référence.** NWSync downloads server content before joining.

**Adaptation obligatoire pour OpenRPG Forge.** Resumable repository client, cache deduplication, verification, trust and user storage controls.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Synchronisation automatique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-010"></a>

### ORF-CONT-010 — Packs curatés

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Distribution |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain` |
| Dépendances | MULTI |
| Sources | S08, S09 |
| Tags | `registry`, `curated`, `package` |

**Comportement Aurora de référence.** EE launcher can surface curated content.

**Adaptation obligatoire pour OpenRPG Forge.** Optional registry with signed metadata, moderation and compatibility; local/manual install remains supported.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Packs curatés » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-011"></a>

### ORF-CONT-011 — Localisation

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S01, S03 |
| Tags | `localization`, `i18n`, `text` |

**Comportement Aurora de référence.** Aurora supports translated strings in multiple languages.

**Adaptation obligatoire pour OpenRPG Forge.** Localization catalogs, ICU-style plural/gender, fallback, pseudo-localization, extraction and translator workflow.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Localisation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-012"></a>

### ORF-CONT-012 — Police TrueType et UI scalable

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE, QA |
| Sources | S08 |
| Tags | `font`, `ui`, `accessibility` |

**Comportement Aurora de référence.** EE adds TTF and UI scaling.

**Adaptation obligatoire pour OpenRPG Forge.** Font assets, fallback chains, shaping, DPI scaling, text size controls and localization testing.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Police TrueType et UI scalable » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-013"></a>

### ORF-CONT-013 — Textures

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE |
| Sources | S03, S06 |
| Tags | `texture`, `pbr`, `import` |

**Comportement Aurora de référence.** Aurora content uses TGA/DDS/PLT and override conventions.

**Adaptation obligatoire pour OpenRPG Forge.** Standard import of PNG/TIFF/EXR/KTX2 etc.; PBR channels, compression profiles, texel density and licensing.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Textures » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-014"></a>

### ORF-CONT-014 — Modèles 3D

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S19 |
| Tags | `model`, `gltf`, `blender` |

**Comportement Aurora de référence.** Aurora uses MDL models; NeverBlender authors models/walkmeshes.

**Adaptation obligatoire pour OpenRPG Forge.** GLTF/GLB canonical interchange, import presets, rigs, animations, collisions, sockets, LOD and validation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Modèles 3D » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-015"></a>

### ORF-CONT-015 — Walkmeshes et collisions

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S19 |
| Tags | `walkmesh`, `collision`, `nav` |

**Comportement Aurora de référence.** Aurora models/tiles include walkmesh data.

**Adaptation obligatoire pour OpenRPG Forge.** Separate navigation/collision assets, generation, manual edit, material tags and platform cooking.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Walkmeshes et collisions » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-016"></a>

### ORF-CONT-016 — Animations

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CREATURE |
| Sources | S19 |
| Tags | `animation`, `rig`, `retarget` |

**Comportement Aurora de référence.** Custom content supports animation sets.

**Adaptation obligatoire pour OpenRPG Forge.** Clips, animation libraries, retarget maps, root motion, events, compression and preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Animations » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-017"></a>

### ORF-CONT-017 — VFX et emitters

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE, QA |
| Sources | S05, S19 |
| Tags | `vfx`, `particles`, `effect` |

**Comportement Aurora de référence.** Aurora has visual effects and custom models/emitters.

**Adaptation obligatoire pour OpenRPG Forge.** VFXGraph/provider abstraction, particles, decals, lights, audio cues, LOD and performance budgets.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « VFX et emitters » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-018"></a>

### ORF-CONT-018 — 2DA-style data tables

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + Kits |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S05 |
| Tags | `data-driven`, `schema`, `table` |

**Comportement Aurora de référence.** Aurora and EE make many rules data-driven through 2DA tables.

**Adaptation obligatoire pour OpenRPG Forge.** Typed data definitions and schemas, references, editor grids, migrations and package overrides; no raw positional tables required.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « 2DA-style data tables » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-019"></a>

### ORF-CONT-019 — Resource namespaces

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `namespace`, `asset-id`, `reference` |

**Comportement Aurora de référence.** Aurora uses resrefs with length/format constraints.

**Adaptation obligatoire pour OpenRPG Forge.** URI-like AssetId `package:type/path`, UUID backing, aliases and collision-free namespaces.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Resource namespaces » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-020"></a>

### ORF-CONT-020 — Content browser

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | PALETTE |
| Sources | S20 |
| Tags | `browser`, `resource`, `search` |

**Comportement Aurora de référence.** Toolset palettes and community tools browse many resource types.

**Adaptation obligatoire pour OpenRPG Forge.** Unified browser with facets, preview, references, source package, license, import status and actions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Content browser » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-021"></a>

### ORF-CONT-021 — External edit round-trip

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem pattern |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | BUILD |
| Sources | S19 |
| Tags | `external-editor`, `reimport`, `watch` |

**Comportement Aurora de référence.** Aurora custom content often edited in external tools.

**Adaptation obligatoire pour OpenRPG Forge.** Open source asset in Blender/audio/image editor, file watcher, controlled reimport and diff of metadata.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « External edit round-trip » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-022"></a>

### ORF-CONT-022 — Blender bridge

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + AI |
| Origine | Ecosystem modernization |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.McpHost` |
| Dépendances | AI, QA |
| Sources | S19 |
| Tags | `blender`, `bridge`, `security` |

**Comportement Aurora de référence.** NeverBlender offers specialized authoring.

**Adaptation obligatoire pour OpenRPG Forge.** Typed Python bridge for primitives, validation, LOD, collision, export; no arbitrary code execution through MCP.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Blender bridge » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-023"></a>

### ORF-CONT-023 — Provenance d’asset

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AI, QA |
| Sources | S22 |
| Tags | `provenance`, `license`, `asset` |

**Comportement Aurora de référence.** Historical packs often lack uniform licensing metadata.

**Adaptation obligatoire pour OpenRPG Forge.** SPDX/license file, author, source URL, permission evidence, modifications, AI model/prompt policy and hashes.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Provenance d’asset » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-024"></a>

### ORF-CONT-024 — Quarantaine d’import

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `quarantine`, `security`, `import` |

**Comportement Aurora de référence.** External resources may be malformed or malicious.

**Adaptation obligatoire pour OpenRPG Forge.** Import into sandboxed staging, size/type limits, malware scanning adapter, parser hardening and manual approval.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Quarantaine d’import » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-025"></a>

### ORF-CONT-025 — Compatibilité de plateforme

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `platform`, `variant`, `build` |

**Comportement Aurora de référence.** Assets may need different compression or shaders.

**Adaptation obligatoire pour OpenRPG Forge.** Platform variants selected by build profile, with fallback and parity validation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Compatibilité de plateforme » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-026"></a>

### ORF-CONT-026 — Migrations de package

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `migration`, `package`, `version` |

**Comportement Aurora de référence.** Patches historically require manual content updates.

**Adaptation obligatoire pour OpenRPG Forge.** Package migration scripts in trusted host code, dry-run, backups, changelog and compatibility tests.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Migrations de package » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-027"></a>

### ORF-CONT-027 — Désinstallation sûre

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `uninstall`, `dependency`, `impact` |

**Comportement Aurora de référence.** Removing a HAK can break module references.

**Adaptation obligatoire pour OpenRPG Forge.** Impact analysis, dependent packages, replacement mapping, backup and refusal if unresolved.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Désinstallation sûre » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-028"></a>

### ORF-CONT-028 — Signatures

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core + Network |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | MULTI |
| Sources | Conception Forge |
| Tags | `signature`, `trust`, `package` |

**Comportement Aurora de référence.** NWSync uses hashes; modern distribution benefits from signatures.

**Adaptation obligatoire pour OpenRPG Forge.** Optional package signing, trusted keys, revocation metadata and unsigned-content policy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Signatures » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-029"></a>

### ORF-CONT-029 — Delta updates

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | MULTI |
| Sources | S14 |
| Tags | `delta`, `update`, `content` |

**Comportement Aurora de référence.** Large custom content benefits from incremental distribution.

**Adaptation obligatoire pour OpenRPG Forge.** Chunk/content-addressed delta, resume, dedup and rollback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Delta updates » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-030"></a>

### ORF-CONT-030 — Credits and notices generation

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `credits`, `license`, `automation` |

**Comportement Aurora de référence.** Module authors manually include credits.

**Adaptation obligatoire pour OpenRPG Forge.** Generate NOTICE, licenses UI and per-asset attribution from manifests, with missing-license build failures.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Credits and notices generation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-cont-031"></a>

### ORF-CONT-031 — IP boundary enforcement

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Governance |
| Origine | Legal boundary |
| Composants probables | `Forge.Domain` |
| Dépendances | QA |
| Sources | S22 |
| Tags | `ip`, `license`, `compliance` |

**Comportement Aurora de référence.** NWN EULA limits use of proprietary variations/assets outside NWN.

**Adaptation obligatoire pour OpenRPG Forge.** No importer/pack may redistribute NWN/CEP proprietary resources; compatibility work is isolated and requires legal review.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « IP boundary enforcement » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.
