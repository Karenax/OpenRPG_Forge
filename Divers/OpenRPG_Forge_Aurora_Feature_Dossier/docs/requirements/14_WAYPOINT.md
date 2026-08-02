# Waypoints, patrouilles, ancres et navigation

**Domaine : `WAYPOINT`**  
**Préfixe : `ORF-WPT`**  
**Couche dominante : Forge Core**  
**Nombre d’exigences : 17**  
**Sources principales : S03, S02, S07, S08, S11**

## Finalité

Repères nommés, map pins, points de spawn, parcours de patrouille, routines inter-zones, routes aériennes et outils de navigation.

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
| [ORF-WPT-001](#orf-wpt-001) | Waypoint nommé | P0 | Native Aurora |
| [ORF-WPT-002](#orf-wpt-002) | Apparence d’éditeur | P1 | Native Aurora |
| [ORF-WPT-003](#orf-wpt-003) | Map note | P1 | Native Aurora |
| [ORF-WPT-004](#orf-wpt-004) | Map note activable | P1 | Native Aurora |
| [ORF-WPT-005](#orf-wpt-005) | Point de départ | P0 | Native Aurora |
| [ORF-WPT-006](#orf-wpt-006) | Destination de transition | P0 | Native Aurora |
| [ORF-WPT-007](#orf-wpt-007) | Points de patrouille ordonnés | P1 | Native Aurora + expansion |
| [ORF-WPT-008](#orf-wpt-008) | Patrouille forward/reverse | P1 | Native Aurora |
| [ORF-WPT-009](#orf-wpt-009) | Orientation au waypoint | P1 | Native Aurora modernized |
| [ORF-WPT-010](#orf-wpt-010) | Patrouille inter-zones | P2 | Official expansion |
| [ORF-WPT-011](#orf-wpt-011) | Routes aériennes | P2 | Forge extension |
| [ORF-WPT-012](#orf-wpt-012) | Routes de véhicules | P3 | Forge extension |
| [ORF-WPT-013](#orf-wpt-013) | Ancres de caméra | P2 | Forge extension |
| [ORF-WPT-014](#orf-wpt-014) | Ancres audio/VFX | P1 | Native Aurora modernized |
| [ORF-WPT-015](#orf-wpt-015) | Groupes et requêtes | P1 | Native Aurora modernized |
| [ORF-WPT-016](#orf-wpt-016) | Visualisation des routes | P1 | Forge extension |
| [ORF-WPT-017](#orf-wpt-017) | Validation | P0 | Forge extension |

## Exigences détaillées

<a id="orf-wpt-001"></a>

### ORF-WPT-001 — Waypoint nommé

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `waypoint`, `anchor`, `identity` |

**Comportement Aurora de référence.** Aurora utilise nom et tag pour référencer une position.

**Adaptation obligatoire pour OpenRPG Forge.** AnchorDefinition/Instance avec UUID, slug, transform, type, tags et scope.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Waypoint nommé » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-002"></a>

### ORF-WPT-002 — Apparence d’éditeur

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SHELL |
| Sources | S03 |
| Tags | `gizmo`, `icon`, `editor` |

**Comportement Aurora de référence.** Aurora affiche différents drapeaux/flèches.

**Adaptation obligatoire pour OpenRPG Forge.** Gizmo iconique par type, couleur, couche, visibilité et taille écran constante.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Apparence d’éditeur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-003"></a>

### ORF-WPT-003 — Map note

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | AREA, QUEST |
| Sources | S03 |
| Tags | `map`, `pin`, `waypoint` |

**Comportement Aurora de référence.** Un waypoint peut créer un pin sur la carte.

**Adaptation obligatoire pour OpenRPG Forge.** MapMarker avec texte, icône, étage, catégorie, précision, visibility policy et tracking.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Map note » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-004"></a>

### ORF-WPT-004 — Map note activable

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `reveal`, `map`, `state` |

**Comportement Aurora de référence.** Aurora peut cacher puis activer un pin par script.

**Adaptation obligatoire pour OpenRPG Forge.** Marker visibility par joueur/parti/monde, reveal event, animation et persistence.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Map note activable » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-005"></a>

### ORF-WPT-005 — Point de départ

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PROJECT |
| Sources | S03 |
| Tags | `spawn`, `start`, `anchor` |

**Comportement Aurora de référence.** Les waypoints servent aux starts et respawns.

**Adaptation obligatoire pour OpenRPG Forge.** SpawnAnchor avec profile, clearance, priority, team, orientation et fallback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Point de départ » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-006"></a>

### ORF-WPT-006 — Destination de transition

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | DOOR, TRIGGER |
| Sources | S03 |
| Tags | `portal`, `destination`, `anchor` |

**Comportement Aurora de référence.** Portes/triggers ciblent un waypoint.

**Adaptation obligatoire pour OpenRPG Forge.** PortalDestination validée, cross-zone, orientation, velocity policy et preload.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Destination de transition » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-007"></a>

### ORF-WPT-007 — Points de patrouille ordonnés

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora + expansion |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S02, S03 |
| Tags | `patrol`, `route`, `schedule` |

**Comportement Aurora de référence.** Waypoint Walking utilise une suite de waypoints.

**Adaptation obligatoire pour OpenRPG Forge.** PatrolRoute graph/spline avec ordre, wait, facing, speed, action et branch conditions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Points de patrouille ordonnés » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-008"></a>

### ORF-WPT-008 — Patrouille forward/reverse

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `patrol`, `traversal`, `route` |

**Comportement Aurora de référence.** Aurora supporte plusieurs modes de parcours.

**Adaptation obligatoire pour OpenRPG Forge.** TraversalPolicy loop, ping-pong, once, random, weighted et scripted.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Patrouille forward/reverse » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-009"></a>

### ORF-WPT-009 — Orientation au waypoint

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `facing`, `arrival`, `behavior` |

**Comportement Aurora de référence.** Variable historique règle facing.

**Adaptation obligatoire pour OpenRPG Forge.** ArrivalBehavior: face direction/target, animation, wait, interact et callback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Orientation au waypoint » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-010"></a>

### ORF-WPT-010 — Patrouille inter-zones

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Official expansion |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | AREA, MULTI |
| Sources | S02, S03 |
| Tags | `patrol`, `cross-area`, `streaming` |

**Comportement Aurora de référence.** Waypoint Walking peut franchir des transitions.

**Adaptation obligatoire pour OpenRPG Forge.** Route segment cross-zone avec portal link, streaming, ownership et reprise après chargement.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Patrouille inter-zones » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-011"></a>

### ORF-WPT-011 — Routes aériennes

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | Conception Forge |
| Tags | `flight`, `route`, `3d` |

**Comportement Aurora de référence.** Aurora est centré sur walkmesh.

**Adaptation obligatoire pour OpenRPG Forge.** FlightRoute 3D avec altitude, courbes, banking, obstacle policy et debug.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Routes aériennes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-012"></a>

### ORF-WPT-012 — Routes de véhicules

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, MULTI |
| Sources | Conception Forge |
| Tags | `vehicle`, `route`, `spline` |

**Comportement Aurora de référence.** Aucun outil natif général.

**Adaptation obligatoire pour OpenRPG Forge.** VehicleRoute avec lanes, speed limits, stops, spline, overtaking policy et network sync.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Routes de véhicules » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-013"></a>

### ORF-WPT-013 — Ancres de caméra

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | Conception Forge |
| Tags | `camera`, `anchor`, `cinematic` |

**Comportement Aurora de référence.** Les waypoints peuvent être utilisés par scripts.

**Adaptation obligatoire pour OpenRPG Forge.** CameraAnchor/ShotAnchor avec framing, lens, look-at, duration et safe area.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Ancres de caméra » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-014"></a>

### ORF-WPT-014 — Ancres audio/VFX

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AUDIO |
| Sources | S15 |
| Tags | `locator`, `vfx`, `audio` |

**Comportement Aurora de référence.** Les scripts ciblent souvent des tags de position.

**Adaptation obligatoire pour OpenRPG Forge.** Generic Locator pour sons, effets, spawns, objectives et gameplay, avec typed purpose.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Ancres audio/VFX » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-015"></a>

### ORF-WPT-015 — Groupes et requêtes

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S15 |
| Tags | `query`, `collection`, `anchor` |

**Comportement Aurora de référence.** Les tags servent à trouver une série de waypoints.

**Adaptation obligatoire pour OpenRPG Forge.** AnchorCollection par tags/query, ordre stable, nearest/random deterministic et editor preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Groupes et requêtes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-016"></a>

### ORF-WPT-016 — Visualisation des routes

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `debug`, `route`, `visualization` |

**Comportement Aurora de référence.** Aurora montre les waypoints mais peu de diagnostic.

**Adaptation obligatoire pour OpenRPG Forge.** Afficher segments, sens, coûts, actions, broken links, nav projection et timing.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Visualisation des routes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-wpt-017"></a>

### ORF-WPT-017 — Validation

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `waypoint`, `route` |

**Comportement Aurora de référence.** Tags dupliqués ou destinations cassées posent problème.

**Adaptation obligatoire pour OpenRPG Forge.** Vérifier IDs, routes, destinations, clearance, nav, cross-zone links et map localization.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

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
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Validation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
