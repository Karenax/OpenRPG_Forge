# Coque du Toolset et ergonomie d’édition

**Domaine : `SHELL`**  
**Préfixe : `ORF-SHELL`**  
**Couche dominante : Forge Core**  
**Nombre d’exigences : 24**  
**Sources principales : S01, S03, S04, S13**

## Finalité

Fenêtres d’édition, navigation du projet, vues, sélection, filtres, historique, construction, test et documentation contextuelle.

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
| [ORF-SHELL-001](#orf-shell-001) | Disposition multi-panneaux du Toolset | P0 | Native Aurora |
| [ORF-SHELL-002](#orf-shell-002) | Explorateur hiérarchique du projet | P0 | Native Aurora |
| [ORF-SHELL-003](#orf-shell-003) | Vue 3D principale | P0 | Native Aurora |
| [ORF-SHELL-004](#orf-shell-004) | Palette contextuelle | P0 | Native Aurora |
| [ORF-SHELL-005](#orf-shell-005) | Palettes Standard et Custom | P0 | Native Aurora |
| [ORF-SHELL-006](#orf-shell-006) | Sélection et inspection | P0 | Native Aurora |
| [ORF-SHELL-007](#orf-shell-007) | Navigation caméra d’édition | P0 | Native Aurora |
| [ORF-SHELL-008](#orf-shell-008) | Filtres d’affichage | P1 | Native Aurora |
| [ORF-SHELL-009](#orf-shell-009) | Placement répété | P1 | Native Aurora |
| [ORF-SHELL-010](#orf-shell-010) | Retour visuel de placement invalide | P0 | Native Aurora |
| [ORF-SHELL-011](#orf-shell-011) | Transformation avec gizmos | P0 | Native Aurora + EE |
| [ORF-SHELL-012](#orf-shell-012) | Édition multi-zones par onglets | P1 | Enhanced Edition |
| [ORF-SHELL-013](#orf-shell-013) | Undo et redo complets | P0 | Native Aurora + EE |
| [ORF-SHELL-014](#orf-shell-014) | Copier, couper, coller et dupliquer | P0 | Native Aurora |
| [ORF-SHELL-015](#orf-shell-015) | Recherche globale | P0 | Native Aurora extended |
| [ORF-SHELL-016](#orf-shell-016) | Find references | P0 | Forge extension |
| [ORF-SHELL-017](#orf-shell-017) | Renommage sûr | P0 | Forge extension |
| [ORF-SHELL-018](#orf-shell-018) | Aperçu et mode test | P0 | Native Aurora modernized |
| [ORF-SHELL-019](#orf-shell-019) | Build du module | P0 | Native Aurora |
| [ORF-SHELL-020](#orf-shell-020) | Messages, erreurs et statut | P0 | Native Aurora modernized |
| [ORF-SHELL-021](#orf-shell-021) | Aide contextuelle | P1 | Forge extension |
| [ORF-SHELL-022](#orf-shell-022) | Raccourcis configurables | P2 | Native Aurora + EE |
| [ORF-SHELL-023](#orf-shell-023) | Profils d’éditeur | P1 | Forge extension |
| [ORF-SHELL-024](#orf-shell-024) | Sauvegarde automatique et récupération | P1 | Forge extension |

## Exigences détaillées

<a id="orf-shell-001"></a>

### ORF-SHELL-001 — Disposition multi-panneaux du Toolset

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03 |
| Tags | `editor`, `docking`, `workspace` |

**Comportement Aurora de référence.** Aurora sépare arborescence du module, vue principale et palette d’objets.

**Adaptation obligatoire pour OpenRPG Forge.** Créer un espace Forge à panneaux ancrables avec explorateur, viewport, inspecteur, palette, diagnostics et assistant IA.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Disposition multi-panneaux du Toolset » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-002"></a>

### ORF-SHELL-002 — Explorateur hiérarchique du projet

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03 |
| Tags | `project-tree`, `search`, `selection` |

**Comportement Aurora de référence.** L’arbre de module classe zones, conversations, scripts et objets placés.

**Adaptation obligatoire pour OpenRPG Forge.** Fournir une arborescence virtuelle par type, dossier, zone, tag, dépendance et statut de validation.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Explorateur hiérarchique du projet » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-003"></a>

### ORF-SHELL-003 — Vue 3D principale

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S01, S03 |
| Tags | `viewport`, `3d`, `camera` |

**Comportement Aurora de référence.** La zone ouverte est construite et inspectée dans une vue 3D orbitable.

**Adaptation obligatoire pour OpenRPG Forge.** Viewport Godot intégré avec modes édition, simulation, éclairage, navigation, collisions et performance.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Vue 3D principale » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-004"></a>

### ORF-SHELL-004 — Palette contextuelle

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S01, S03 |
| Tags | `palette`, `catalog`, `assets` |

**Comportement Aurora de référence.** La palette présente tuiles, créatures, objets, portes, rencontres, sons, triggers, waypoints et marchands.

**Adaptation obligatoire pour OpenRPG Forge.** Catalogue filtré par type, kit, style, licence, compatibilité, tags et favoris.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Palette contextuelle » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-005"></a>

### ORF-SHELL-005 — Palettes Standard et Custom

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE, CONTENT |
| Sources | S03 |
| Tags | `palette`, `scope`, `provenance` |

**Comportement Aurora de référence.** Aurora distingue contenu livré et blueprints du module.

**Adaptation obligatoire pour OpenRPG Forge.** Distinguer packs installés, contenu du projet, contenu généré, favoris et contenu temporaire.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Palettes Standard et Custom » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-006"></a>

### ORF-SHELL-006 — Sélection et inspection

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03 |
| Tags | `selection`, `inspector`, `multi-edit` |

**Comportement Aurora de référence.** Un objet sélectionné ouvre ses propriétés et peut être manipulé.

**Adaptation obligatoire pour OpenRPG Forge.** Sélection simple/multiple, focus, cadre, verrouillage, inspection multi-objet et édition des propriétés communes.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sélection et inspection » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-007"></a>

### ORF-SHELL-007 — Navigation caméra d’édition

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03 |
| Tags | `camera`, `viewport`, `bookmarks` |

**Comportement Aurora de référence.** Zoom, rotation et translation permettent d’inspecter une zone.

**Adaptation obligatoire pour OpenRPG Forge.** Navigation configurable souris/clavier, vitesse adaptative, focus sélection, vues orthographiques, bookmarks et caméra pilote.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Navigation caméra d’édition » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-008"></a>

### ORF-SHELL-008 — Filtres d’affichage

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03 |
| Tags | `filters`, `layers`, `visibility` |

**Comportement Aurora de référence.** Le Toolset peut masquer certaines catégories pour réduire l’encombrement.

**Adaptation obligatoire pour OpenRPG Forge.** Calques, filtres par type/tag/auteur/état, isolation de sélection, ghost mode et profils mémorisés.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Filtres d’affichage » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-009"></a>

### ORF-SHELL-009 — Placement répété

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `placement`, `brush`, `undo` |

**Comportement Aurora de référence.** Aurora permet de placer plusieurs instances d’un blueprint, notamment avec Shift.

**Adaptation obligatoire pour OpenRPG Forge.** Mode pinceau, répétition, distribution, alignement, scatter déterministe et annulation groupée.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Placement répété » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-010"></a>

### ORF-SHELL-010 — Retour visuel de placement invalide

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `placement`, `validation`, `preview` |

**Comportement Aurora de référence.** Aurora signale les emplacements impossibles.

**Adaptation obligatoire pour OpenRPG Forge.** Prévisualisation fantôme colorée, cause détaillée, collision, socket, grille, budget et correction automatique proposée.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Retour visuel de placement invalide » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-011"></a>

### ORF-SHELL-011 — Transformation avec gizmos

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora + EE |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S04 |
| Tags | `transform`, `gizmo`, `snap` |

**Comportement Aurora de référence.** Le Toolset et EE permettent rotation, position et transformations visuelles.

**Adaptation obligatoire pour OpenRPG Forge.** Translation, rotation, échelle, pivot, coordonnées locales/globales, snap, saisie numérique et alignements.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

#### ForgeScript

- Pas d’API gameplay obligatoire.
- Les opérations de build/validation sont accessibles au CLI et, lorsque pertinent, au MCP.

#### MCP et agents

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Transformation avec gizmos » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-012"></a>

### ORF-SHELL-012 — Édition multi-zones par onglets

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S04 |
| Tags | `tabs`, `multi-document`, `area` |

**Comportement Aurora de référence.** EE ajoute plusieurs onglets de zones.

**Adaptation obligatoire pour OpenRPG Forge.** Onglets, split view, comparaison, copie inter-zones, contexte actif explicite et protection contre l’édition du mauvais document.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Édition multi-zones par onglets » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-013"></a>

### ORF-SHELL-013 — Undo et redo complets

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora + EE |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S04, S10 |
| Tags | `undo`, `redo`, `commands`, `transactions` |

**Comportement Aurora de référence.** Aurora/EE offre un historique, amélioré au fil des patches.

**Adaptation obligatoire pour OpenRPG Forge.** Toutes les mutations humaines et IA passent par des commandes atomiques sérialisables et annulables.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Undo et redo complets » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-014"></a>

### ORF-SHELL-014 — Copier, couper, coller et dupliquer

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `clipboard`, `duplicate`, `references` |

**Comportement Aurora de référence.** Les objets et propriétés peuvent être copiés et dupliqués.

**Adaptation obligatoire pour OpenRPG Forge.** Clipboard sémantique avec nouveaux UUID, remapping des références, options instance ou copie, et aperçu du résultat.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Copier, couper, coller et dupliquer » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-015"></a>

### ORF-SHELL-015 — Recherche globale

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora extended |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S03, S15 |
| Tags | `search`, `index`, `references` |

**Comportement Aurora de référence.** Aurora propose diverses recherches dans conversations et ressources.

**Adaptation obligatoire pour OpenRPG Forge.** Recherche plein texte et structurée dans données, scripts, graphes, assets, tags, diagnostics et références inverses.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Recherche globale » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-016"></a>

### ORF-SHELL-016 — Find references

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S17, S20 |
| Tags | `references`, `impact-analysis` |

**Comportement Aurora de référence.** Les tags/resrefs historiques nécessitent des recherches manuelles ou outils tiers.

**Adaptation obligatoire pour OpenRPG Forge.** Afficher tous les usages d’un objet, script, asset, texte, variable ou état avant modification/suppression.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Find references » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-017"></a>

### ORF-SHELL-017 — Renommage sûr

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S17, S20 |
| Tags | `rename`, `uuid`, `migration` |

**Comportement Aurora de référence.** Le renommage historique par tag/resref est fragile.

**Adaptation obligatoire pour OpenRPG Forge.** Renommer slug, nom ou chemin sans changer l’UUID et migrer toutes les références transactionnellement.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Renommage sûr » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-018"></a>

### ORF-SHELL-018 — Aperçu et mode test

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, QA |
| Sources | S01, S03 |
| Tags | `preview`, `playtest`, `headless` |

**Comportement Aurora de référence.** Aurora permet de construire puis tester le module.

**Adaptation obligatoire pour OpenRPG Forge.** Play From Here, Play Selected Quest, Play Dialogue, simulation headless et retour à l’état d’édition.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Aperçu et mode test » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-019"></a>

### ORF-SHELL-019 — Build du module

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD, SCRIPT, CONTENT |
| Sources | S01, S03, S04 |
| Tags | `build`, `compile`, `cache` |

**Comportement Aurora de référence.** Le Toolset compile scripts et assemble les ressources.

**Adaptation obligatoire pour OpenRPG Forge.** Pipeline build incrémental, validation, compilation ForgeScript, import d’assets, cache et rapport reproductible.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Build du module » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-020"></a>

### ORF-SHELL-020 — Messages, erreurs et statut

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S01, S13 |
| Tags | `diagnostics`, `logs`, `status` |

**Comportement Aurora de référence.** Le Toolset dispose de fenêtres de messages et d’état.

**Adaptation obligatoire pour OpenRPG Forge.** Console structurée avec erreurs cliquables, catégories, corrélation de transaction, logs runtime et export de diagnostic.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Messages, erreurs et statut » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-021"></a>

### ORF-SHELL-021 — Aide contextuelle

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT, AI |
| Sources | S01, S02, S03 |
| Tags | `help`, `docs`, `assistant` |

**Comportement Aurora de référence.** Aurora s’appuie sur manuels, assistants et descriptions.

**Adaptation obligatoire pour OpenRPG Forge.** Documentation intégrée, exemples ForgeScript, description des propriétés, liens de dépendance et aide IA sourcée.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Aide contextuelle » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-022"></a>

### ORF-SHELL-022 — Raccourcis configurables

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora + EE |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S04 |
| Tags | `keyboard`, `accessibility`, `commands` |

**Comportement Aurora de référence.** Aurora utilise menus et raccourcis fixes, EE ajoute des touches d’inventaire.

**Adaptation obligatoire pour OpenRPG Forge.** Keymap éditable, profils Aurora/Blender/standard, conflits détectés, commandes recherchables.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Raccourcis configurables » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-023"></a>

### ORF-SHELL-023 — Profils d’éditeur

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | DM, AI |
| Sources | Conception Forge |
| Tags | `profiles`, `roles`, `ux` |

**Comportement Aurora de référence.** L’interface historique montre beaucoup d’outils à la fois.

**Adaptation obligatoire pour OpenRPG Forge.** Profils Builder, Writer, Scripter, Artist, GM, Tester et Beginner avec panneaux et permissions adaptés.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Profils d’éditeur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-shell-024"></a>

### ORF-SHELL-024 — Sauvegarde automatique et récupération

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `autosave`, `recovery`, `history` |

**Comportement Aurora de référence.** Les outils historiques peuvent perdre du travail lors d’un crash.

**Adaptation obligatoire pour OpenRPG Forge.** Autosave versionné, journal de commandes, récupération après crash, snapshots et comparaison avant restauration.

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

- Exposer la sélection, le document actif et les diagnostics; ne pas piloter arbitrairement l’UI.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sauvegarde automatique et récupération » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
